using ETOMS_API.DTOs.Tournament;
using ETOMS_API.Models;
using ETOMS_API.Repositories.Interface;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Services.Impl;

// Vòng đời giải (mục 28, góp ý nhóm 18/11). Trước đây không chỗ nào chuyển giải sang ONGOING / COMPLETED nên giải đá xong
// chung kết vẫn "Sắp diễn ra" và bảng xếp hạng không bao giờ được cộng (không ai ghi Registration.FinalRank).
//   - Bắt đầu: nhánh đã khóa. Tự động khi tới giờ khai mạc; BTC bấm "Bắt đầu giải" để bắt đầu sớm hơn.
//   - Kết thúc: mọi trận đã xong. Tự động khi xác định được hạng chung cuộc; vòng tròn có đội bằng nhau ở hạng 1–2 thì BTC chọn thứ tự.
//     Kết thúc ghi FinalRank + FinalPlacement cho mọi đội được duyệt (dùng cho bảng xếp hạng RNK).
//   - Chưa đủ đội: đã đóng đăng ký, chưa khóa nhánh, số đội duyệt dưới mức tối thiểu để sinh nhánh. BTC gia hạn đăng ký
//     (đổi hạn đóng đăng ký và khai mạc) hoặc hủy giải. Đủ tối thiểu nhưng chưa đầy vẫn sinh nhánh được (có đội miễn đấu).
// BTC không sửa trạng thái tùy ý: đặt "Đã kết thúc" khi chưa đá là cộng điểm bảng xếp hạng sai.
public class TournamentLifecycleService : ITournamentLifecycleService
{
    private readonly IUnitOfWork _uow;
    private readonly IGroupStandingService _standings;

    public TournamentLifecycleService(IUnitOfWork uow, IGroupStandingService standings)
    {
        _uow = uow;
        _standings = standings;
    }

    public async Task<int?> GetOrganizerAccountIdAsync(int tournamentId)
        => await _uow.Repository<Tournament>().Query().Where(t => t.TournamentId == tournamentId)
            .Select(t => (int?)t.OrganizerAccountId).FirstOrDefaultAsync();

    public async Task<TournamentLifecycleDto?> GetAsync(int tournamentId)
    {
        var t = await _uow.Repository<Tournament>().GetByIdAsync(tournamentId);
        return t is null ? null : await BuildAsync(t, null);
    }

    // ===== Bắt đầu giải =====
    public async Task<Result<TournamentLifecycleDto>> StartAsync(int tournamentId, int organizerAccountId)
    {
        using var opLock = TournamentOpLock.TryEnter(tournamentId);
        if (opLock is null) return Result<TournamentLifecycleDto>.Failure(TournamentOpLock.BusyMessage, StatusCodes.Status409Conflict);
        var t = await _uow.Repository<Tournament>().GetByIdAsync(tournamentId);
        if (t is null) return Result<TournamentLifecycleDto>.Failure("Không tìm thấy giải đấu.", StatusCodes.Status404NotFound);
        if (t.OrganizerAccountId != organizerAccountId) return Result<TournamentLifecycleDto>.Failure("Chỉ Ban tổ chức của giải được bắt đầu giải.", StatusCodes.Status403Forbidden);
        var blocked = StartBlocked(t);
        if (blocked is not null) return Result<TournamentLifecycleDto>.Failure(blocked);

        var notified = await StartCoreAsync(t, DateTime.UtcNow);
        await _uow.SaveChangesAsync();
        return Result<TournamentLifecycleDto>.Success(await BuildAsync(t, null), $"Giải đã bắt đầu. Đã báo cho {notified} đội trưởng.");
    }

    // ===== Kết thúc giải =====
    public async Task<Result<TournamentLifecycleDto>> FinishAsync(int tournamentId, int organizerAccountId, IReadOnlyList<int>? tieOrder)
    {
        using var opLock = TournamentOpLock.TryEnter(tournamentId);
        if (opLock is null) return Result<TournamentLifecycleDto>.Failure(TournamentOpLock.BusyMessage, StatusCodes.Status409Conflict);
        var t = await _uow.Repository<Tournament>().GetByIdAsync(tournamentId);
        if (t is null) return Result<TournamentLifecycleDto>.Failure("Không tìm thấy giải đấu.", StatusCodes.Status404NotFound);
        if (t.OrganizerAccountId != organizerAccountId) return Result<TournamentLifecycleDto>.Failure("Chỉ Ban tổ chức của giải được kết thúc giải.", StatusCodes.Status403Forbidden);
        var statusBlocked = FinishStatusBlocked(t);
        if (statusBlocked is not null) return Result<TournamentLifecycleDto>.Failure(statusBlocked);

        var data = await LoadAsync(t);
        var outcome = Evaluate(t, data, tieOrder);
        if (outcome.Blocked is not null) return Result<TournamentLifecycleDto>.Failure(outcome.Blocked);

        await FinishCoreAsync(t, data, outcome.Ranks, DateTime.UtcNow);
        await _uow.SaveChangesAsync();
        return Result<TournamentLifecycleDto>.Success(await BuildAsync(t, null), "Giải đã kết thúc. Hạng chung cuộc đã được ghi vào bảng xếp hạng.");
    }

    // ===== Gia hạn / mở lại đăng ký =====
    public async Task<Result<TournamentLifecycleDto>> ExtendRegistrationAsync(int tournamentId, int organizerAccountId, ExtendRegistrationRequest r)
    {
        using var opLock = TournamentOpLock.TryEnter(tournamentId);
        if (opLock is null) return Result<TournamentLifecycleDto>.Failure(TournamentOpLock.BusyMessage, StatusCodes.Status409Conflict);
        var t = await _uow.Repository<Tournament>().GetByIdAsync(tournamentId);
        if (t is null) return Result<TournamentLifecycleDto>.Failure("Không tìm thấy giải đấu.", StatusCodes.Status404NotFound);
        if (t.OrganizerAccountId != organizerAccountId) return Result<TournamentLifecycleDto>.Failure("Chỉ Ban tổ chức của giải được gia hạn đăng ký.", StatusCodes.Status403Forbidden);
        var now = DateTime.UtcNow;
        var blocked = ExtendBlocked(t, now);
        if (blocked is not null) return Result<TournamentLifecycleDto>.Failure(blocked);

        if (r.RegCloseAt is not DateTime closeRaw) return Result<TournamentLifecycleDto>.FieldFailure("RegCloseAt", "Chọn hạn đóng đăng ký mới.");
        if (r.StartAt is not DateTime startRaw) return Result<TournamentLifecycleDto>.FieldFailure("StartAt", "Chọn thời gian khai mạc mới.");
        var close = ToUtc(closeRaw);
        var start = ToUtc(startRaw);
        DateTime? end = r.EndAt is DateTime e ? ToUtc(e) : null;
        if (close <= now.AddMinutes(1)) return Result<TournamentLifecycleDto>.FieldFailure("RegCloseAt", "Hạn đóng đăng ký mới phải ở tương lai.");
        // Giữ nguyên thời gian kết thúc cũ nếu còn hợp lệ; cũ hơn khai mạc mới thì BTC phải chọn lại.
        if (end is null && t.EndAt is DateTime oldEnd && Utc(oldEnd) > start) end = Utc(oldEnd);
        if (TournamentTimeline.Check(Utc(t.RegOpenAt), close, start, end) is { } bad)
            return Result<TournamentLifecycleDto>.FieldFailure(bad.Field, bad.Message);

        t.RegCloseAt = close;
        t.StartAt = start;
        t.EndAt = end;
        var hadBracket = await _uow.Repository<BracketNode>().AnyAsync(n => n.TournamentId == t.TournamentId);

        var captains = await CaptainsAsync(t.TournamentId, new[] { "PENDING", "APPROVED", "WAITLISTED" });
        foreach (var c in captains)
            await _uow.Repository<Notification>().AddAsync(Note(c, "Giải gia hạn đăng ký",
                $"Ban tổ chức giải {t.Name} gia hạn đăng ký đến {Vn(close)}. Khai mạc lúc {Vn(start)}.", "/bracket/view.html?id=" + t.TournamentId, now));
        await _uow.SaveChangesAsync();

        var msg = $"Đã gia hạn đăng ký đến {Vn(close)}, khai mạc {Vn(start)}. Đã báo cho {captains.Count} đội trưởng.";
        if (hadBracket) msg += " Nhánh đấu nháp đã sinh trước đó cần sinh lại sau khi đóng đăng ký mới.";
        return Result<TournamentLifecycleDto>.Success(await BuildAsync(t, null), msg);
    }

    // ===== Chạy định kỳ =====
    public async Task<int> RunScheduledAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var ids = await _uow.Repository<Tournament>().Query()
            .Where(t => t.BracketLockedAt != null && (t.Status == "PUBLISHED" || t.Status == "ONGOING"))
            .Select(t => t.TournamentId).ToListAsync(ct);
        var changed = 0;
        foreach (var id in ids)
        {
            ct.ThrowIfCancellationRequested();
            using var opLock = TournamentOpLock.TryEnter(id);
            if (opLock is null) continue;   // BTC đang thao tác trên giải này — lượt sau làm
            var t = await _uow.Repository<Tournament>().GetByIdAsync(id);
            if (t is null || t.BracketLockedAt is null) continue;

            var data = await LoadAsync(t);
            var outcome = Evaluate(t, data, null);
            if (outcome.Blocked is null)
            {
                await FinishCoreAsync(t, data, outcome.Ranks, now);
                await _uow.SaveChangesAsync(ct);
                changed++;
            }
            else if (t.Status == "PUBLISHED" && Utc(t.StartAt) <= now)
            {
                await StartCoreAsync(t, now);
                await _uow.SaveChangesAsync(ct);
                changed++;
            }
        }
        return changed;
    }

    public async Task<bool> RecomputeCompletedAsync(int tournamentId)
    {
        var t = await _uow.Repository<Tournament>().GetByIdAsync(tournamentId);
        if (t is null || t.Status != "COMPLETED") return false;
        var data = await LoadAsync(t);
        var outcome = Evaluate(t, data, null);
        if (outcome.Blocked is not null) return false;   // vd còn trận bị hủy kết quả / bằng điểm cần BTC chọn
        var changed = new List<int>();
        foreach (var r in data.Approved)
        {
            if (!outcome.Ranks.TryGetValue(r.TeamId, out var rank) || r.FinalRank == rank) continue;
            r.FinalRank = rank;
            r.FinalPlacement = TournamentLifecycle.Placement(rank);
            _uow.Repository<Registration>().Update(r);
            changed.Add(r.TeamId);
        }
        if (changed.Count == 0) return false;
        var now = DateTime.UtcNow;
        foreach (var c in (await CaptainsAsync(tournamentId, new[] { "APPROVED" })).Where(c => changed.Contains(c.TeamId)))
            await _uow.Repository<Notification>().AddAsync(Note(c, "Thứ hạng giải đã được tính lại",
                $"Kết quả giải {t.Name} thay đổi sau phán quyết. Đội {c.TeamName} nay xếp hạng {outcome.Ranks[c.TeamId]}; điểm bảng xếp hạng đã cập nhật.",
                "/bracket/view.html?id=" + tournamentId, now));
        await _uow.SaveChangesAsync();
        return true;
    }

    // ---------------------------------------------------------------- Điều kiện

    private static string? StartBlocked(Tournament t) => t.Status switch
    {
        "DRAFT" => "Giải còn là bản nháp.",
        "ONGOING" => "Giải đã bắt đầu.",
        "COMPLETED" => "Giải đã kết thúc.",
        "CANCELLED" => "Giải đã bị hủy.",
        _ => t.BracketLockedAt is null ? "Cần sinh và khóa nhánh đấu trước khi bắt đầu giải." : null
    };

    private static string? FinishStatusBlocked(Tournament t) => t.Status switch
    {
        "DRAFT" => "Giải còn là bản nháp.",
        "COMPLETED" => "Giải đã kết thúc.",
        "CANCELLED" => "Giải đã bị hủy.",
        _ => t.BracketLockedAt is null ? "Nhánh đấu chưa khóa nên giải chưa thể kết thúc." : null
    };

    private static string? ExtendBlocked(Tournament t, DateTime now)
    {
        if (t.Status != "PUBLISHED") return t.Status == "DRAFT" ? "Giải còn là bản nháp — sửa thời gian ở trang Sửa giải." : "Giải đã bắt đầu, kết thúc hoặc đã hủy.";
        if (t.BracketLockedAt is not null) return "Nhánh đấu đã khóa nên không gia hạn đăng ký được.";
        if (now < Utc(t.RegCloseAt)) return "Giải vẫn đang nhận đăng ký. Muốn đổi hạn thì sửa ở trang Sửa giải.";
        return null;
    }

    // ---------------------------------------------------------------- Dữ liệu

    private sealed class Data
    {
        public List<int> TeamIds { get; init; } = new();
        public List<TournamentLifecycle.Node> Nodes { get; init; } = new();
        public List<TournamentLifecycle.MatchRow> Matches { get; init; } = new();
        public List<TournamentLifecycle.GroupRow> GroupRows { get; init; } = new();
        public List<Registration> Approved { get; init; } = new();
    }

    private async Task<Data> LoadAsync(Tournament t)
    {
        var approved = await _uow.Repository<Registration>().Query()
            .Where(r => r.TournamentId == t.TournamentId && r.Status == "APPROVED").ToListAsync();
        var nodes = await _uow.Repository<BracketNode>().Query().Where(n => n.TournamentId == t.TournamentId)
            .Select(n => new TournamentLifecycle.Node(n.BracketNodeId, n.StageType, n.StageNo, n.RoundNo, n.PositionInRound, n.ParentNodeId, n.IsBye))
            .ToListAsync();
        var matches = await _uow.Repository<Match>().Query().Where(m => m.TournamentId == t.TournamentId)
            .Select(m => new TournamentLifecycle.MatchRow(m.BracketNodeId, m.TeamAid, m.TeamBid, m.WinnerTeamId, m.Status))
            .ToListAsync();
        var rows = new List<TournamentLifecycle.GroupRow>();
        if (t.Format is "ROUND_ROBIN" or "HYBRID" && nodes.Any(n => n.StageType == "GROUP"))
        {
            var board = await _standings.GetStandingsAsync(t.TournamentId, t.OrganizerAccountId);
            foreach (var g in board?.Groups ?? new())
                foreach (var r in g.Rows)
                    rows.Add(new TournamentLifecycle.GroupRow(g.GroupLabel, r.TeamId, r.Position, r.Points, r.Diff, r.GamesWon));
        }
        return new Data { TeamIds = approved.Select(r => r.TeamId).ToList(), Nodes = nodes, Matches = matches, GroupRows = rows, Approved = approved };
    }

    private static TournamentLifecycle.Outcome Evaluate(Tournament t, Data d, IReadOnlyList<int>? tieOrder)
        => TournamentLifecycle.Evaluate(t.Format, t.HasThirdPlaceMatch, d.TeamIds, d.Nodes, d.Matches, d.GroupRows, tieOrder);

    private async Task<int> StartCoreAsync(Tournament t, DateTime now)
    {
        t.Status = "ONGOING";
        _uow.Repository<Tournament>().Update(t);
        var captains = await CaptainsAsync(t.TournamentId, new[] { "APPROVED" });
        foreach (var c in captains)
            await _uow.Repository<Notification>().AddAsync(Note(c, "Giải đấu đã bắt đầu",
                $"Giải {t.Name} đã bắt đầu. Theo dõi lịch thi đấu và điểm danh đúng giờ.", "/bracket/view.html?id=" + t.TournamentId, now));
        return captains.Count;
    }

    private async Task FinishCoreAsync(Tournament t, Data d, Dictionary<int, int> ranks, DateTime now)
    {
        t.Status = "COMPLETED";
        if (Utc(t.StartAt) <= now && (t.EndAt is null || Utc(t.EndAt.Value) > now)) t.EndAt = now;   // ghi ngày kết thúc thật
        _uow.Repository<Tournament>().Update(t);
        foreach (var r in d.Approved)
        {
            if (!ranks.TryGetValue(r.TeamId, out var rank)) continue;
            r.FinalRank = rank;
            r.FinalPlacement = TournamentLifecycle.Placement(rank);
            _uow.Repository<Registration>().Update(r);
        }
        var names = await _uow.Repository<Team>().Query().Where(x => d.TeamIds.Contains(x.TeamId)).ToDictionaryAsync(x => x.TeamId, x => x.Name);
        var champion = ranks.FirstOrDefault(x => x.Value == 1).Key;
        var captains = await CaptainsAsync(t.TournamentId, new[] { "APPROVED" });
        foreach (var c in captains)
        {
            var mine = ranks.TryGetValue(c.TeamId, out var rk) ? $" Đội {c.TeamName} xếp hạng {rk}." : "";
            await _uow.Repository<Notification>().AddAsync(Note(c, "Giải đấu đã kết thúc",
                $"Giải {t.Name} đã kết thúc. Vô địch: {names.GetValueOrDefault(champion, "—")}.{mine}", "/bracket/view.html?id=" + t.TournamentId, now));
        }
    }

    private sealed record Captain(int AccountId, int TeamId, string TeamName);

    private async Task<List<Captain>> CaptainsAsync(int tournamentId, string[] statuses)
        => (await _uow.Repository<Registration>().Query()
                .Where(r => r.TournamentId == tournamentId && statuses.Contains(r.Status))
                .Select(r => new { r.Team.CaptainAccountId, r.TeamId, r.Team.Name })
                .ToListAsync())
            .GroupBy(x => x.CaptainAccountId).Select(g => g.First())
            .Select(x => new Captain(x.CaptainAccountId, x.TeamId, x.Name)).ToList();

    // Kênh IN_APP: bộ gửi email chung (NotificationEmailInterceptor) tự gửi thêm email cho người bật nhận email.
    private static Notification Note(Captain c, string title, string body, string link, DateTime now) => new()
    {
        AccountId = c.AccountId, NotificationType = "SYSTEM", Title = title, Body = body, LinkUrl = link,
        Channel = "IN_APP", IsRead = false, SentAt = now
    };

    // ---------------------------------------------------------------- Thẻ tiến trình

    private async Task<TournamentLifecycleDto> BuildAsync(Tournament t, IReadOnlyList<int>? tieOrder)
    {
        var now = DateTime.UtcNow;
        var data = await LoadAsync(t);
        var approved = data.TeamIds.Count;
        var locked = t.BracketLockedAt is not null;
        var (label, cls) = TournamentLifecycle.PublicLabel(t.Status, locked, Utc(t.RegOpenAt), Utc(t.RegCloseAt), now, approved, t.Format);
        var min = TournamentLifecycle.MinTeams(t.Format);
        var dto = new TournamentLifecycleDto
        {
            TournamentId = t.TournamentId,
            Status = t.Status,
            Label = label,
            LabelClass = cls,
            ApprovedTeams = approved,
            MinTeams = min,
            MaxTeams = t.MaxTeams,
            RegistrationOpen = TournamentLifecycle.IsRegistrationOpen(t.Status, locked, Utc(t.RegOpenAt), Utc(t.RegCloseAt), now),
            ShortOfTeams = TournamentLifecycle.IsShortOfTeams(t.Status, locked, Utc(t.RegCloseAt), now, approved, t.Format),
            HasBracket = data.Nodes.Count > 0,
            BracketLocked = locked,
            MatchesTotal = data.Matches.Count,
            MatchesFinished = data.Matches.Count(m => TournamentLifecycle.IsMatchFinished(m.Status)),
            RegOpenAt = t.RegOpenAt, RegCloseAt = t.RegCloseAt, StartAt = t.StartAt, EndAt = t.EndAt
        };
        dto.StartBlocked = StartBlocked(t);
        dto.CanStart = dto.StartBlocked is null;
        dto.ExtendBlocked = ExtendBlocked(t, now);
        dto.CanExtend = dto.ExtendBlocked is null;
        var names = await _uow.Repository<Team>().Query().Where(x => data.TeamIds.Contains(x.TeamId)).ToDictionaryAsync(x => x.TeamId, x => x.Name);

        if (t.Status == "COMPLETED")
        {
            dto.FinishBlocked = "Giải đã kết thúc.";
            dto.Podium = data.Approved.Where(r => r.FinalRank is >= 1 and <= 3).OrderBy(r => r.FinalRank)
                .Select(r => new FinalRankDto { Rank = r.FinalRank!.Value, TeamId = r.TeamId, Name = names.GetValueOrDefault(r.TeamId, "—") }).ToList();
        }
        else
        {
            dto.FinishBlocked = FinishStatusBlocked(t);
            if (dto.FinishBlocked is null)
            {
                var outcome = Evaluate(t, data, tieOrder);
                dto.FinishBlocked = outcome.TiedTop.Count > 0 ? null : outcome.Blocked;
                dto.TiedTop = outcome.TiedTop.Select(id => new TieTeamDto { TeamId = id, Name = names.GetValueOrDefault(id, "—") }).ToList();
            }
            dto.CanFinish = dto.FinishBlocked is null;
        }

        dto.Hint = t.Status switch
        {
            "DRAFT" => "Giải còn là bản nháp. Hoàn tất thiết lập rồi công bố giải.",
            "CANCELLED" => "Giải đã bị hủy.",
            "COMPLETED" => "Giải đã kết thúc. Hạng chung cuộc đã được ghi vào bảng xếp hạng.",
            "ONGOING" => dto.CanFinish
                ? (dto.TiedTop.Count > 0 ? "Mọi trận đã xong nhưng các đội đứng đầu đang bằng nhau. Chọn thứ tự rồi bấm Kết thúc giải." : "Mọi trận đã xong. Hệ thống sẽ tự kết thúc giải trong ít phút, hoặc bấm Kết thúc giải.")
                : $"Giải đang diễn ra: {dto.MatchesFinished}/{dto.MatchesTotal} trận đã xong. Giải tự kết thúc khi trận cuối có kết quả.",
            _ when dto.RegistrationOpen => $"Đang nhận đăng ký: {approved}/{t.MaxTeams} đội đã duyệt, đóng đăng ký lúc {Vn(t.RegCloseAt)}.",
            _ when now < Utc(t.RegOpenAt) => $"Mở đăng ký lúc {Vn(t.RegOpenAt)}.",
            _ when dto.ShortOfTeams => $"Đã đóng đăng ký nhưng mới có {approved} đội được duyệt, cần ít nhất {min} đội để sinh nhánh. Gia hạn đăng ký hoặc hủy giải.",
            _ when !dto.HasBracket => $"Đã đóng đăng ký với {approved}/{t.MaxTeams} đội. Sinh nhánh đấu" + (approved < t.MaxTeams ? " (đội thiếu sẽ thành lượt miễn đấu)" : "") + " hoặc gia hạn đăng ký.",
            _ when !locked => "Nhánh đấu đang là bản nháp. Kiểm tra rồi khóa nhánh đấu.",
            _ => $"Nhánh đã khóa. Giải tự bắt đầu lúc khai mạc {Vn(t.StartAt)}, hoặc bấm Bắt đầu giải để bắt đầu ngay."
        };
        return dto;
    }

    private static DateTime Utc(DateTime d) => DateTime.SpecifyKind(d, DateTimeKind.Utc);
    private static DateTime ToUtc(DateTime d) => d.Kind == DateTimeKind.Utc ? d : d.Kind == DateTimeKind.Local ? d.ToUniversalTime() : DateTime.SpecifyKind(d, DateTimeKind.Utc);
    private static string Vn(DateTime utc) => Utc(utc).AddHours(7).ToString("HH:mm dd/MM/yyyy");
}
