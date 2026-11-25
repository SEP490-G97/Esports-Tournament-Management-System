using ETOMS_API.DTOs.Discipline;
using ETOMS_API.Models;
using ETOMS_API.Repositories.Interface;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Services.Impl;

// Module PEN — Report 3 UC-34 (Issue and Cancel Penalty), UC-35 (Disqualify on Penalty Threshold) và phần
// "loại đội + xử mọi trận còn lại" dùng chung với UC-24. Lược đồ có sẵn: Penalty (Severity, Points, Status,
// CancelsPenaltyID, CancelReason), Tournament.PenaltyThreshold, Registration.Status = DISQUALIFIED,
// AuditLog ISSUE_PENALTY / CANCEL_PENALTY / DISQUALIFY / WALKOVER — không thêm bảng, không đổi lược đồ.
// BR-PEN-01: không xóa thẻ phạt; hủy = một dòng CANCELLATION trỏ về thẻ gốc, thẻ gốc chuyển CANCELLED, cả hai còn hiển thị.
// BR-PEN-02: tổng điểm = điểm các thẻ ACTIVE không phải dòng hủy (thẻ đã hủy và dòng hủy đều không tính).
// BR-PEN-03: dòng hủy phải nêu thẻ bị hủy và có lý do riêng (CHECK CK_Penalty_Cancel).
public sealed class DisciplineService : IDisciplineService
{
    public static readonly string[] IssuableSeverities = ["WARNING", "MINOR", "MAJOR"];
    public const int MaxPoints = 100;
    private static readonly string[] OpenMatchStatuses = ["PENDING", "READY", "LIVE", "WAITING_REPORT"];

    private readonly IUnitOfWork _uow;
    private readonly IResultService _results;

    public DisciplineService(IUnitOfWork uow, IResultService results)
    {
        _uow = uow;
        _results = results;
    }

    private IQueryable<T> Q<T>() where T : class => _uow.Repository<T>().Query();

    public static string SeverityLabel(string severity) => severity switch
    {
        "WARNING" => "Cảnh cáo",
        "MINOR" => "Lỗi nhẹ",
        "MAJOR" => "Lỗi nặng",
        "DISQUALIFICATION" => "Tước quyền thi đấu",
        "CANCELLATION" => "Hủy thẻ phạt",
        _ => severity
    };

    private static string PenaltyStatusLabel(string status) => status switch
    {
        "ACTIVE" => "Đang hiệu lực",
        "CANCELLED" => "Đã hủy",
        "EXPIRED" => "Hết hiệu lực",
        _ => status
    };

    // Điểm đang tính của từng đội trong giải (BR-PEN-02), đọc từ view vw_TeamPenaltyPoint (Report 4 §III.2.2): tổng Points
    // của các dòng ACTIVE. Thẻ đã hủy mang Status CANCELLED; dòng hủy và dòng tước quyền mang 0 điểm nên không làm đổi tổng.
    private async Task<Dictionary<int, int>> ActivePointsAsync(int tournamentId, CancellationToken ct)
        => await Q<VwTeamPenaltyPoint>().AsNoTracking()
            .Where(v => v.TournamentId == tournamentId)
            .ToDictionaryAsync(v => v.TeamId, v => (int)(v.ActivePoints ?? 0), ct);

    private Task<bool> IsRefereeOfTeamAsync(int tournamentId, int teamId, int accountId, CancellationToken ct)
        => Q<Match>().AnyAsync(m => m.TournamentId == tournamentId && m.RefereeAccountId == accountId
            && (m.TeamAid == teamId || m.TeamBid == teamId), ct);

    // ===== Bảng kỷ luật của giải (BTC hoặc trọng tài của giải) =====
    public async Task<Result<DisciplineBoardDto>> GetBoardAsync(int tournamentId, int viewerAccountId, CancellationToken ct = default)
    {
        var t = await Q<Tournament>().AsNoTracking().FirstOrDefaultAsync(x => x.TournamentId == tournamentId, ct);
        if (t is null) return Result.Failure<DisciplineBoardDto>("Không tìm thấy giải đấu.", StatusCodes.Status404NotFound);
        var isOrganizer = t.OrganizerAccountId == viewerAccountId;
        var refereeTeams = isOrganizer ? new HashSet<int>() : (await Q<Match>().AsNoTracking()
                .Where(m => m.TournamentId == tournamentId && m.RefereeAccountId == viewerAccountId)
                .Select(m => new { m.TeamAid, m.TeamBid }).ToListAsync(ct))
            .SelectMany(m => new[] { m.TeamAid, m.TeamBid }).Where(x => x.HasValue).Select(x => x!.Value).ToHashSet();
        var isReferee = !isOrganizer && await Q<Match>().AnyAsync(m => m.TournamentId == tournamentId && m.RefereeAccountId == viewerAccountId, ct);
        if (!isOrganizer && !isReferee)
            return Result.Failure<DisciplineBoardDto>("Chỉ ban tổ chức hoặc trọng tài của giải mới xem được bảng kỷ luật.", StatusCodes.Status403Forbidden);

        var ongoing = t.Status == "ONGOING";
        var points = await ActivePointsAsync(tournamentId, ct);
        var regs = await Q<Registration>().AsNoTracking()
            .Where(r => r.TournamentId == tournamentId && (r.Status == "APPROVED" || r.Status == "DISQUALIFIED"))
            .Select(r => new { r.TeamId, r.Team.Name, r.Status })
            .OrderBy(r => r.Name).ToListAsync(ct);

        var dto = new DisciplineBoardDto
        {
            TournamentId = t.TournamentId,
            TournamentName = t.Name,
            TournamentStatus = StatusText.Tournament(t.Status),
            PenaltyThreshold = t.PenaltyThreshold,
            IsOrganizer = isOrganizer,
            CanIssue = ongoing,
            CanSetThreshold = isOrganizer && (t.Status is "DRAFT" or "PUBLISHED"),
            Teams = regs.Select(r => new TeamPenaltyTotalDto
            {
                TeamId = r.TeamId,
                TeamName = r.Name,
                RegistrationStatus = r.Status,
                RegistrationStatusLabel = StatusText.Registration(r.Status),
                ActivePoints = points.GetValueOrDefault(r.TeamId),
                CanIssue = ongoing && r.Status == "APPROVED" && (isOrganizer || refereeTeams.Contains(r.TeamId))
            }).ToList()
        };
        dto.Penalties = await LoadPenaltiesAsync(Q<Penalty>().Where(p => p.TournamentId == tournamentId), ct);
        foreach (var p in dto.Penalties)
            p.CanCancel = ongoing && p.Status == "ACTIVE" && IssuableSeverities.Contains(p.Severity)
                && (isOrganizer || refereeTeams.Contains(p.TeamId));
        return Result.Success(dto);
    }

    private static async Task<List<PenaltyDto>> LoadPenaltiesAsync(IQueryable<Penalty> query, CancellationToken ct)
    {
        var rows = await query.AsNoTracking()
            .OrderByDescending(p => p.IssuedAt).ThenByDescending(p => p.PenaltyId)
            .Select(p => new PenaltyDto
            {
                PenaltyId = p.PenaltyId,
                TournamentId = p.TournamentId,
                TournamentName = p.Tournament.Name,
                TeamId = p.TeamId,
                TeamName = p.Team.Name,
                MatchId = p.MatchId,
                Severity = p.Severity,
                Points = p.Points,
                Reason = p.Reason,
                Status = p.Status,
                IssuedByName = p.IssuedByAccount.FullName,
                IssuedAt = p.IssuedAt,
                CancelsPenaltyId = p.CancelsPenaltyId,
                CancelReason = p.CancelReason,
                CancelledByPenaltyId = p.InverseCancelsPenalty != null ? p.InverseCancelsPenalty.PenaltyId : null
            }).ToListAsync(ct);
        foreach (var r in rows)
        {
            r.SeverityLabel = SeverityLabel(r.Severity);
            r.StatusLabel = r.Severity == "CANCELLATION" ? "Dòng hủy" : PenaltyStatusLabel(r.Status);
        }
        return rows;
    }

    public async Task<TeamDisciplineRecordDto> GetTeamRecordAsync(int teamId, CancellationToken ct = default)
    {
        // Hồ sơ công khai: giải nháp không lộ ra ngoài.
        var penalties = await LoadPenaltiesAsync(
            Q<Penalty>().Where(p => p.TeamId == teamId && p.Tournament.Status != "DRAFT"), ct);
        return new TeamDisciplineRecordDto { TeamId = teamId, Penalties = penalties };
    }

    // ===== UC-34: cấp thẻ phạt =====
    public async Task<Result> IssueAsync(int tournamentId, int actorAccountId, IssuePenaltyRequest request, CancellationToken ct = default)
    {
        var reason = (request.Reason ?? "").Trim();
        if (reason.Length == 0) return Result.FieldFailure(nameof(request.Reason), "Vui lòng nhập lý do cấp thẻ phạt.");   // MSG-PEN-001
        if (reason.Length > 1000) return Result.FieldFailure(nameof(request.Reason), "Lý do tối đa 1000 ký tự.");
        var severity = (request.Severity ?? "").Trim().ToUpperInvariant();
        if (!IssuableSeverities.Contains(severity))
            return Result.FieldFailure(nameof(request.Severity), "Mức phạt phải là Cảnh cáo, Lỗi nhẹ hoặc Lỗi nặng.");
        if (request.Points < 0 || request.Points > MaxPoints)
            return Result.FieldFailure(nameof(request.Points), $"Điểm phạt từ 0 đến {MaxPoints}.");

        var t = await Q<Tournament>().FirstOrDefaultAsync(x => x.TournamentId == tournamentId, ct);
        if (t is null) return Result.Failure("Không tìm thấy giải đấu.", StatusCodes.Status404NotFound);
        var isOrganizer = t.OrganizerAccountId == actorAccountId;
        if (!isOrganizer && !await IsRefereeOfTeamAsync(tournamentId, request.TeamId, actorAccountId, ct))
            return Result.Failure("Chỉ ban tổ chức của giải hoặc trọng tài được phân công trận của đội mới được cấp thẻ phạt.", StatusCodes.Status403Forbidden);
        if (t.Status != "ONGOING") return Result.Failure("Chỉ cấp thẻ phạt khi giải đang diễn ra.", StatusCodes.Status409Conflict);

        var reg = await Q<Registration>().Include(r => r.Team)
            .FirstOrDefaultAsync(r => r.TournamentId == tournamentId && r.TeamId == request.TeamId, ct);
        if (reg is null || reg.Status != "APPROVED")
            return Result.FieldFailure(nameof(request.TeamId), "Đội không còn thi đấu trong giải này.");
        if (request.MatchId is int mid && !await Q<Match>().AnyAsync(m => m.MatchId == mid && m.TournamentId == tournamentId
                && (m.TeamAid == request.TeamId || m.TeamBid == request.TeamId), ct))
            return Result.FieldFailure(nameof(request.MatchId), "Trận không thuộc giải hoặc đội không thi đấu trận này.");

        var now = DateTime.UtcNow;
        var penalty = new Penalty
        {
            TournamentId = tournamentId,
            TeamId = request.TeamId,
            MatchId = request.MatchId,
            Severity = severity,
            Points = request.Points,
            Reason = reason,
            IssuedByAccountId = actorAccountId,
            Status = "ACTIVE",
            IssuedAt = now
        };
        await _uow.Repository<Penalty>().AddAsync(penalty);
        await _uow.SaveChangesAsync(ct);

        var total = (await ActivePointsAsync(tournamentId, ct)).GetValueOrDefault(request.TeamId);
        await _uow.Repository<AuditLog>().AddAsync(new AuditLog
        {
            ActorAccountId = actorAccountId, TournamentId = tournamentId, ActionType = "ISSUE_PENALTY",
            ObjectType = "PENALTY", ObjectId = penalty.PenaltyId,
            AfterValue = $"TeamId={request.TeamId}; Severity={severity}; Points={request.Points}; Total={total}",
            Reason = reason, CreatedAt = now
        });
        var thresholdText = t.PenaltyThreshold is int th ? $"/{th}" : "";
        await NotifyAsync(reg.Team.CaptainAccountId, "Đội bị cấp thẻ phạt",
            $"Đội {reg.Team.Name} nhận thẻ {SeverityLabel(severity).ToLowerInvariant()} ({request.Points} điểm) ở giải {t.Name}. Lý do: {reason}. Tổng điểm phạt hiện tại: {total}{thresholdText}.",
            $"/team/details.html?id={reg.TeamId}");
        await _uow.SaveChangesAsync(ct);

        // UC-34 A1 → UC-35: đủ ngưỡng thì loại ngay (bộ hẹn giờ vẫn quét lại định kỳ).
        if (t.PenaltyThreshold is int threshold && total >= threshold)
        {
            await DisqualifyOnThresholdAsync(t, request.TeamId, total, ct);
            return Result.Success("Đội đã đủ điểm phạt và bị tước quyền thi đấu.");   // MSG-PEN-002
        }
        return Result.Success($"Đã cấp thẻ phạt. Tổng điểm phạt của đội: {total}{thresholdText}.");
    }

    // ===== UC-34 A2: hủy thẻ phạt bằng dòng liên kết =====
    public async Task<Result> CancelAsync(int tournamentId, int penaltyId, int actorAccountId, CancelPenaltyRequest request, CancellationToken ct = default)
    {
        var reason = (request.Reason ?? "").Trim();
        if (reason.Length == 0) return Result.FieldFailure(nameof(request.Reason), "Vui lòng nhập lý do hủy thẻ phạt.");
        if (reason.Length > 500) return Result.FieldFailure(nameof(request.Reason), "Lý do hủy tối đa 500 ký tự.");

        var original = await Q<Penalty>().Include(p => p.Tournament).Include(p => p.Team)
            .FirstOrDefaultAsync(p => p.PenaltyId == penaltyId && p.TournamentId == tournamentId, ct);
        if (original is null) return Result.Failure("Không tìm thấy thẻ phạt.", StatusCodes.Status404NotFound);
        var isOrganizer = original.Tournament.OrganizerAccountId == actorAccountId;
        if (!isOrganizer && !await IsRefereeOfTeamAsync(tournamentId, original.TeamId, actorAccountId, ct))
            return Result.Failure("Chỉ ban tổ chức của giải hoặc trọng tài được phân công trận của đội mới được hủy thẻ phạt.", StatusCodes.Status403Forbidden);
        if (original.Tournament.Status != "ONGOING") return Result.Failure("Chỉ hủy thẻ phạt khi giải đang diễn ra.", StatusCodes.Status409Conflict);
        if (!IssuableSeverities.Contains(original.Severity))
            return Result.Failure("Không thể hủy dòng này (dòng hủy hoặc quyết định tước quyền thi đấu).", StatusCodes.Status409Conflict);
        if (original.Status != "ACTIVE") return Result.Failure("Thẻ phạt này đã bị hủy hoặc hết hiệu lực.", StatusCodes.Status409Conflict);

        var now = DateTime.UtcNow;
        original.Status = "CANCELLED";
        var cancellation = new Penalty
        {
            TournamentId = tournamentId,
            TeamId = original.TeamId,
            MatchId = original.MatchId,
            Severity = "CANCELLATION",
            Points = 0,
            Reason = reason,
            CancelReason = reason,
            CancelsPenaltyId = original.PenaltyId,
            IssuedByAccountId = actorAccountId,
            Status = "ACTIVE",
            IssuedAt = now
        };
        await _uow.Repository<Penalty>().AddAsync(cancellation);
        await _uow.SaveChangesAsync(ct);   // UX_Penalty_CancelsOnce: một thẻ chỉ hủy được một lần

        var total = (await ActivePointsAsync(tournamentId, ct)).GetValueOrDefault(original.TeamId);
        await _uow.Repository<AuditLog>().AddAsync(new AuditLog
        {
            ActorAccountId = actorAccountId, TournamentId = tournamentId, ActionType = "CANCEL_PENALTY",
            ObjectType = "PENALTY", ObjectId = original.PenaltyId,
            BeforeValue = $"Status=ACTIVE; Points={original.Points}",
            AfterValue = $"Status=CANCELLED; CancelledBy={cancellation.PenaltyId}; Total={total}",
            Reason = reason, CreatedAt = now
        });
        await NotifyAsync(original.Team.CaptainAccountId, "Thẻ phạt đã được hủy",
            $"Thẻ {SeverityLabel(original.Severity).ToLowerInvariant()} của đội {original.Team.Name} ở giải {original.Tournament.Name} đã được hủy. Lý do: {reason}. Tổng điểm phạt hiện tại: {total}.",
            $"/team/details.html?id={original.TeamId}");
        await _uow.SaveChangesAsync(ct);
        return Result.Success($"Đã hủy thẻ phạt. Tổng điểm phạt của đội: {total}.");
    }

    // Ngưỡng được công bố trước khi giải bắt đầu (giả định của UC-35: đội đã đọc ngưỡng khi đăng ký).
    public async Task<Result> SetThresholdAsync(int tournamentId, int actorAccountId, SetPenaltyThresholdRequest request, CancellationToken ct = default)
    {
        var t = await Q<Tournament>().FirstOrDefaultAsync(x => x.TournamentId == tournamentId, ct);
        if (t is null) return Result.Failure("Không tìm thấy giải đấu.", StatusCodes.Status404NotFound);
        if (t.OrganizerAccountId != actorAccountId)
            return Result.Failure("Chỉ ban tổ chức của giải mới đặt được ngưỡng điểm phạt.", StatusCodes.Status403Forbidden);
        if (t.Status is not ("DRAFT" or "PUBLISHED"))
            return Result.Failure("Chỉ đặt ngưỡng điểm phạt trước khi giải bắt đầu.", StatusCodes.Status409Conflict);
        if (request.Threshold is int v && (v < 1 || v > MaxPoints))
            return Result.FieldFailure(nameof(request.Threshold), $"Ngưỡng điểm phạt từ 1 đến {MaxPoints}, hoặc để trống nếu không áp dụng.");
        t.PenaltyThreshold = request.Threshold;
        await _uow.SaveChangesAsync(ct);
        return Result.Success(request.Threshold is null ? "Đã bỏ ngưỡng loại tự động." : $"Đã đặt ngưỡng loại tự động: {request.Threshold} điểm phạt.");
    }

    // ===== UC-35 =====
    private async Task DisqualifyOnThresholdAsync(Tournament t, int teamId, int total, CancellationToken ct)
    {
        var ids = await Q<Penalty>().AsNoTracking()
            .Where(p => p.TournamentId == t.TournamentId && p.TeamId == teamId && p.Status == "ACTIVE" && p.Severity != "CANCELLATION" && p.Points > 0)
            .OrderBy(p => p.PenaltyId).Select(p => p.PenaltyId).ToListAsync(ct);
        await DisqualifyTeamAsync(t.TournamentId, teamId, null,
            $"Tự động: đội đạt {total} điểm phạt, chạm ngưỡng {t.PenaltyThreshold} của giải.",
            $"Cause=PENALTY_THRESHOLD; Threshold={t.PenaltyThreshold}; Points={total}; Penalties={string.Join(",", ids)}",
            null, ct);
    }

    public async Task<int> RunScheduledAsync(CancellationToken ct = default)
    {
        var changed = 0;
        // 1) Đội đủ ngưỡng ở giải đang diễn ra.
        var tours = await Q<Tournament>().AsNoTracking()
            .Where(t => t.Status == "ONGOING" && t.PenaltyThreshold != null).ToListAsync(ct);
        foreach (var t in tours)
        {
            var points = await ActivePointsAsync(t.TournamentId, ct);
            var active = await Q<Registration>().AsNoTracking()
                .Where(r => r.TournamentId == t.TournamentId && r.Status == "APPROVED").Select(r => r.TeamId).ToListAsync(ct);
            foreach (var teamId in active.Where(id => points.GetValueOrDefault(id) >= t.PenaltyThreshold!.Value))
            {
                await DisqualifyOnThresholdAsync(t, teamId, points[teamId], ct);
                changed++;
            }
        }
        // 2) Đội đã bị loại còn trận mà nay đối thủ đã rõ (vd đội thắng nhánh khác vừa vào) → xử thắng trắng.
        var pendings = await Q<Match>().AsNoTracking()
            .Where(m => m.Tournament.Status == "ONGOING" && OpenMatchStatuses.Contains(m.Status) && m.TeamAid != null && m.TeamBid != null
                && m.Tournament.Registrations.Any(r => r.Status == "DISQUALIFIED" && (r.TeamId == m.TeamAid || r.TeamId == m.TeamBid)))
            .Select(m => new { m.TournamentId, m.TeamAid, m.TeamBid }).ToListAsync(ct);
        foreach (var g in pendings.Select(m => m.TournamentId).Distinct())
        {
            var dq = await Q<Registration>().AsNoTracking()
                .Where(r => r.TournamentId == g && r.Status == "DISQUALIFIED").Select(r => r.TeamId).ToListAsync(ct);
            foreach (var teamId in dq)
            {
                var (resolved, _, _) = await ResolveRemainingAsync(g, teamId, null, "Đội đã bị tước quyền thi đấu trước đó.", ct);
                changed += resolved.Count;
            }
        }
        return changed;
    }

    // ===== Bộ loại đội dùng chung (UC-24 và UC-35) =====
    public async Task<int> DisqualifyTeamAsync(int tournamentId, int teamId, int? actorAccountId, string reason, string auditAfterValue,
        int? alreadyResolvedMatchId = null, CancellationToken ct = default)
    {
        var t = await Q<Tournament>().AsNoTracking().FirstAsync(x => x.TournamentId == tournamentId, ct);
        var reg = await Q<Registration>().Include(r => r.Team)
            .FirstOrDefaultAsync(r => r.TournamentId == tournamentId && r.TeamId == teamId, ct);
        var wasActive = reg is not null && reg.Status == "APPROVED";
        if (wasActive)
        {
            reg!.Status = "DISQUALIFIED";
            await _uow.SaveChangesAsync(ct);
        }

        var (resolved, inProgress, disputed) = await ResolveRemainingAsync(tournamentId, teamId, actorAccountId, reason, ct);
        if (alreadyResolvedMatchId is int first) resolved.Insert(0, first);

        await _uow.Repository<AuditLog>().AddAsync(new AuditLog
        {
            ActorAccountId = actorAccountId, TournamentId = tournamentId, ActionType = "DISQUALIFY",
            ObjectType = "TEAM", ObjectId = teamId,
            BeforeValue = reg is null ? null : $"Registration={(wasActive ? "APPROVED" : reg.Status)}",
            AfterValue = $"{auditAfterValue}; Registration=DISQUALIFIED; Walkovers={string.Join(",", resolved)}"
                + (inProgress.Count > 0 ? $"; InProgress={string.Join(",", inProgress)}" : "")
                + (disputed.Count > 0 ? $"; LeftDisputed={string.Join(",", disputed)}" : ""),
            Reason = reason.Length > 1000 ? reason[..1000] : reason,
            CreatedAt = DateTime.UtcNow
        });

        var teamName = reg?.Team.Name ?? await Q<Team>().Where(x => x.TeamId == teamId).Select(x => x.Name).FirstAsync(ct);
        var disputedNote = disputed.Count > 0 ? $" {disputed.Count} trận đang tranh chấp được giữ lại để trọng tài phân xử." : "";
        if (reg is not null)
            await NotifyAsync(reg.Team.CaptainAccountId, "Đội bị tước quyền thi đấu",
                $"Đội {teamName} đã bị tước quyền thi đấu ở giải {t.Name}. Lý do: {reason}{disputedNote}",
                $"/team/details.html?id={teamId}");
        if (actorAccountId != t.OrganizerAccountId)
            await NotifyAsync(t.OrganizerAccountId, "Một đội đã bị tước quyền thi đấu",
                $"Đội {teamName} đã bị tước quyền thi đấu ở giải {t.Name}. Lý do: {reason}. Đã xử thắng trắng {resolved.Count} trận.{disputedNote}",
                $"/tournament/discipline.html?id={tournamentId}");
        await _uow.SaveChangesAsync(ct);
        return resolved.Count;
    }

    // Xử thắng trắng cho đối thủ mọi trận chưa kết thúc của đội (cùng engine UC-23: WALKOVER + đẩy đội thắng lên).
    // Lặp vì ở thể thức loại kép đội thua rơi xuống nhánh thua và có thể có trận mới. Trận DISPUTED để nguyên (UC-35 E1).
    private async Task<(List<int> Resolved, List<int> InProgress, List<int> Disputed)> ResolveRemainingAsync(
        int tournamentId, int teamId, int? actorAccountId, string reason, CancellationToken ct)
    {
        var resolved = new List<int>();
        var inProgress = new List<int>();
        var disputed = await Q<Match>().AsNoTracking()
            .Where(m => m.TournamentId == tournamentId && m.Status == "DISPUTED" && (m.TeamAid == teamId || m.TeamBid == teamId))
            .Select(m => m.MatchId).ToListAsync(ct);
        for (var round = 0; round < 64; round++)
        {
            var open = await Q<Match>().Include(m => m.TeamA).Include(m => m.TeamB)
                .Where(m => m.TournamentId == tournamentId && OpenMatchStatuses.Contains(m.Status)
                    && m.TeamAid != null && m.TeamBid != null && (m.TeamAid == teamId || m.TeamBid == teamId))
                .OrderBy(m => m.MatchId).ToListAsync(ct);
            if (open.Count == 0) break;
            foreach (var m in open)
            {
                var winner = m.TeamAid == teamId ? m.TeamBid!.Value : m.TeamAid!.Value;
                var wasInProgress = m.Status is "LIVE" or "WAITING_REPORT";   // UC-35 A2
                if (wasInProgress) inProgress.Add(m.MatchId);
                m.Status = "WALKOVER";
                m.ResultSource = "WALKOVER";
                m.WinnerTeamId = winner;
                await _uow.Repository<AuditLog>().AddAsync(new AuditLog
                {
                    ActorAccountId = actorAccountId, TournamentId = tournamentId, ActionType = "WALKOVER",
                    ObjectType = "MATCH", ObjectId = m.MatchId,
                    AfterValue = $"WinnerTeamId={winner}; DisqualifiedTeamId={teamId}" + (wasInProgress ? "; WasInProgress=true" : ""),
                    Reason = "Đối thủ bị tước quyền thi đấu: " + (reason.Length > 900 ? reason[..900] : reason),
                    CreatedAt = DateTime.UtcNow
                });
                var opponent = m.TeamAid == teamId ? m.TeamB : m.TeamA;
                if (opponent is not null)
                    await NotifyAsync(opponent.CaptainAccountId, "Đội bạn thắng trắng",
                        $"Đối thủ của đội {opponent.Name} đã bị tước quyền thi đấu; đội bạn được xử thắng trắng trận #{m.MatchId}.",
                        $"/match/detail.html?id={m.MatchId}");
                await _uow.SaveChangesAsync(ct);
                await _results.AdvanceWinnerAsync(m.MatchId, winner);
                resolved.Add(m.MatchId);
            }
        }
        return (resolved, inProgress, disputed);
    }

    private Task NotifyAsync(int accountId, string title, string body, string link)
        => _uow.Repository<Notification>().AddAsync(new Notification
        {
            AccountId = accountId,
            NotificationType = "PENALTY",
            Title = title,
            Body = body.Length > 1000 ? body[..1000] : body,
            LinkUrl = link,
            Channel = "IN_APP",   // NotificationEmailInterceptor gửi kèm email nếu người nhận bật
            IsRead = false,
            SentAt = DateTime.UtcNow
        });
}
