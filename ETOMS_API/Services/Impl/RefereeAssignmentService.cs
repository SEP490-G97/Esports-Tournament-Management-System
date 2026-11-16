using ETOMS_API.DTOs.Tournament;
using ETOMS_API.Models;
using ETOMS_API.Repositories;
using ETOMS_API.Repositories.Interface;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Services.Impl;

// Module TOUR-13. Trọng tài lưu ở Match.RefereeAccountId, schema không có bảng trọng tài của giải.
public class RefereeAssignmentService : IRefereeAssignmentService
{
    private const int CandidateLimit = 10;
    private const string UnknownTeam = "Chờ xác định";
    private const string NotLockedMessage = "Nhánh đấu chưa khóa nên chưa gán được trọng tài.";
    private const string EndedMessage = "Giải đã kết thúc hoặc đã hủy, không thể gán trọng tài.";

    private readonly IUnitOfWork _uow;

    public RefereeAssignmentService(IUnitOfWork uow) => _uow = uow;

    public async Task<int?> GetOrganizerAccountIdAsync(int tournamentId)
        => (await _uow.Repository<Tournament>().GetByIdAsync(tournamentId))?.OrganizerAccountId;

    public async Task<RefereeBoardDto?> GetBoardAsync(int tournamentId)
    {
        var tournament = await _uow.Repository<Tournament>().GetByIdAsync(tournamentId);
        if (tournament is null) return null;

        var blockedReason = BlockedReason(tournament);
        var rows = await _uow.Repository<Match>().Query()
            .Where(m => m.TournamentId == tournamentId)
            .OrderBy(m => m.BracketNode.StageNo).ThenBy(m => m.BracketNode.RoundNo).ThenBy(m => m.BracketNode.PositionInRound)
            .Select(m => new
            {
                m.MatchId,
                m.BracketNode.StageNo,
                m.BracketNode.StageType,
                m.BracketNode.RoundNo,
                m.BracketNode.PositionInRound,
                m.BracketNode.ParentNodeId,
                m.BracketNode.GroupLabel,
                TeamA = m.TeamA != null ? m.TeamA.Name : null,
                TeamB = m.TeamB != null ? m.TeamB.Name : null,
                m.ScheduledAt,
                m.Status,
                m.RefereeAccountId,
                RefereeName = m.RefereeAccount != null ? m.RefereeAccount.FullName : null
            })
            .ToListAsync();

        // BRK-09: trận tranh hạng Ba cùng vòng với chung kết, cần nhãn riêng.
        var knockout = rows.Where(m => m.StageType == "KNOCKOUT").ToList();
        var lastStage = knockout.Count == 0 ? 0 : knockout.Max(m => m.StageNo);
        var finalRound = knockout.Where(m => m.StageNo == lastStage).Select(m => m.RoundNo ?? 0).DefaultIfEmpty(0).Max();

        return new RefereeBoardDto
        {
            TournamentId = tournament.TournamentId,
            TournamentName = tournament.Name,
            IsBracketLocked = tournament.BracketLockedAt is not null,
            CanAssign = blockedReason is null,
            BlockedReason = blockedReason,
            Rounds = rows.Where(m => m.RoundNo is not null)
                .Select(m => (m.StageNo, RoundNo: m.RoundNo!.Value))
                .Distinct()
                .Select(r => new RefereeRoundDto { StageNo = r.StageNo, RoundNo = r.RoundNo, Label = $"Giai đoạn {r.StageNo} · Vòng {r.RoundNo}" })
                .ToList(),
            Matches = rows.Select(m => new RefereeMatchDto
            {
                MatchId = m.MatchId,
                RoundLabel = ThirdPlaceMatch.IsNode(m.StageType, m.StageNo, m.RoundNo, m.PositionInRound, m.ParentNodeId, lastStage, finalRound)
                    ? ThirdPlaceMatch.Label
                    : RoundLabel(m.RoundNo, m.GroupLabel),
                TeamAName = m.TeamA ?? UnknownTeam,
                TeamBName = m.TeamB ?? UnknownTeam,
                ScheduledAt = m.ScheduledAt,
                StatusLabel = StatusText.Match(m.Status),
                StatusClass = StatusText.MatchClass(m.Status),
                RefereeAccountId = m.RefereeAccountId,
                RefereeName = m.RefereeName,
                CanAssign = blockedReason is null && IsAssignable(m.Status)
            }).ToList()
        };
    }

    public async Task<IReadOnlyList<RefereeCandidateDto>> SearchCandidatesAsync(string? keyword)
    {
        var text = keyword?.Trim();
        if (string.IsNullOrEmpty(text) || text.Length < 2) return Array.Empty<RefereeCandidateDto>();

        return await _uow.Repository<Account>().Query()
            .Where(a => a.Status == "ACTIVE" && (a.FullName.Contains(text) || a.Email.Contains(text)))
            .OrderBy(a => a.FullName)
            .Take(CandidateLimit)
            .Select(a => new RefereeCandidateDto { AccountId = a.AccountId, FullName = a.FullName, Email = a.Email })
            .ToListAsync();
    }

    // ===== TOUR-13: Gán hoặc bỏ trọng tài của một trận =====
    public async Task<Result> AssignRefereeAsync(int tournamentId, int matchId, int? refereeAccountId)
    {
        var tournament = await _uow.Repository<Tournament>().GetByIdAsync(tournamentId);
        if (tournament is null) return Result.Failure("Không tìm thấy giải đấu.");

        var blockedReason = BlockedReason(tournament);
        if (blockedReason is not null) return Result.Failure(blockedReason);

        var match = await _uow.Repository<Match>().Query()
            .FirstOrDefaultAsync(m => m.MatchId == matchId && m.TournamentId == tournamentId);
        if (match is null) return Result.Failure("Không tìm thấy trận đấu trong giải này.");
        if (!IsAssignable(match.Status)) return Result.Failure("Trận này không còn đổi được trọng tài.");

        if (refereeAccountId is null)
        {
            match.RefereeAccountId = null;
            await _uow.SaveChangesAsync();
            return Result.Success("Đã bỏ trọng tài. Quyền phân xử trận này thuộc Ban tổ chức.");
        }

        if (match.RefereeAccountId == refereeAccountId) return Result.Success("Trận đã có trọng tài này.");

        var referee = await _uow.Repository<Account>().GetByIdAsync(refereeAccountId);
        if (referee is null) return Result.Failure("Tài khoản không tồn tại.");
        if (referee.Status != "ACTIVE") return Result.Failure("Tài khoản này không thể làm trọng tài.");

        var conflict = await FindConflictAsync(match, referee.AccountId);
        if (conflict is not null) return Result.Failure(conflict);

        match.RefereeAccountId = referee.AccountId;
        await AddNotificationsAsync(tournament, match, referee);
        await _uow.SaveChangesAsync();
        return Result.Success("Đã gán trọng tài.");
    }

    // ===== TOUR-13: Gán một trọng tài cho cả vòng. Trận vướng quy tắc thì bỏ qua và báo lại =====
    public async Task<Result<AssignRoundResultDto>> AssignRoundAsync(int tournamentId, AssignRoundRefereeRequest request)
    {
        var tournament = await _uow.Repository<Tournament>().GetByIdAsync(tournamentId);
        if (tournament is null) return Result<AssignRoundResultDto>.Failure("Không tìm thấy giải đấu.");

        var blockedReason = BlockedReason(tournament);
        if (blockedReason is not null) return Result<AssignRoundResultDto>.Failure(blockedReason);

        var referee = await _uow.Repository<Account>().GetByIdAsync(request.RefereeAccountId);
        if (referee is null) return Result<AssignRoundResultDto>.Failure("Tài khoản không tồn tại.");
        if (referee.Status != "ACTIVE") return Result<AssignRoundResultDto>.Failure("Tài khoản này không thể làm trọng tài.");

        var matches = await _uow.Repository<Match>().Query()
            .Where(m => m.TournamentId == tournamentId
                && m.BracketNode.StageNo == request.StageNo && m.BracketNode.RoundNo == request.RoundNo)
            .OrderBy(m => m.BracketNode.PositionInRound)
            .ToListAsync();
        if (matches.Count == 0) return Result<AssignRoundResultDto>.Failure("Vòng này không có trận nào.");

        var result = new AssignRoundResultDto();
        var takenSlots = new HashSet<DateTime>();
        foreach (var match in matches)
        {
            if (!IsAssignable(match.Status) || match.RefereeAccountId == referee.AccountId) continue;

            var label = await MatchLabelAsync(match);
            var conflict = await FindConflictAsync(match, referee.AccountId);
            if (conflict is null && match.ScheduledAt is DateTime slot && !takenSlots.Add(slot))
                conflict = "Trọng tài này đã được gán vào trận khác cùng giờ.";

            if (conflict is not null)
            {
                result.SkippedMatches.Add($"{label}: {conflict}");
                continue;
            }

            match.RefereeAccountId = referee.AccountId;
            await AddNotificationsAsync(tournament, match, referee);
            result.AssignedCount++;
        }

        if (result.AssignedCount > 0) await _uow.SaveChangesAsync();

        var message = result.SkippedMatches.Count > 0
            ? $"Đã gán {result.AssignedCount} trận, bỏ qua {result.SkippedMatches.Count} trận."
            : $"Đã gán {result.AssignedCount} trận.";
        return Result<AssignRoundResultDto>.Success(result, message);
    }

    private static string? BlockedReason(Tournament t)
    {
        if (t.Status is "COMPLETED" or "CANCELLED") return EndedMessage;
        return t.BracketLockedAt is null ? NotLockedMessage : null;
    }

    // Trận đã có kết quả thì không đổi. Trận đang tranh chấp giữ nguyên người đang xử.
    private static bool IsAssignable(string matchStatus)
        => matchStatus is "PENDING" or "READY" or "LIVE" or "WAITING_REPORT";

    private static string RoundLabel(int? roundNo, string? groupLabel)
    {
        var round = roundNo is null ? "Chưa xếp vòng" : $"Vòng {roundNo}";
        return string.IsNullOrEmpty(groupLabel) ? round : $"Bảng {groupLabel} · {round}";
    }

    // MAT-13 / BR-MATCH-07: trọng tài không được thuộc đội thi đấu, và không xử hai trận cùng giờ.
    private async Task<string?> FindConflictAsync(Match match, int refereeAccountId)
    {
        var ownTeam = await _uow.Repository<TeamMember>().Query()
            .Where(m => m.AccountId == refereeAccountId && m.Status == "ACTIVE"
                && (m.TeamId == match.TeamAid || m.TeamId == match.TeamBid))
            .Select(m => m.Team.Name)
            .FirstOrDefaultAsync();
        if (ownTeam is not null)
            return $"Trọng tài không được là thành viên của một trong hai đội thi đấu. Người này thuộc đội {ownTeam}.";

        if (match.ScheduledAt is null) return null;

        var busy = await _uow.Repository<Match>().AnyAsync(m =>
            m.MatchId != match.MatchId && m.RefereeAccountId == refereeAccountId && m.ScheduledAt == match.ScheduledAt
            && (m.Status == "PENDING" || m.Status == "READY" || m.Status == "LIVE" || m.Status == "WAITING_REPORT"));
        return busy ? "Trọng tài này đã được gán vào trận khác cùng giờ." : null;
    }

    private async Task<string> MatchLabelAsync(Match match)
    {
        var ids = new[] { match.TeamAid, match.TeamBid };
        var names = await _uow.Repository<Team>().Query()
            .Where(t => ids.Contains(t.TeamId))
            .Select(t => new { t.TeamId, t.Name })
            .ToListAsync();

        string NameOf(int? id) => names.FirstOrDefault(n => n.TeamId == id)?.Name ?? UnknownTeam;
        return $"{NameOf(match.TeamAid)} vs {NameOf(match.TeamBid)}";
    }

    // UC-17 bước 6: báo cho trọng tài và đội trưởng hai đội, theo cài đặt nhận thông báo của họ.
    private async Task AddNotificationsAsync(Tournament tournament, Match match, Account referee)
    {
        var label = await MatchLabelAsync(match);
        var now = DateTime.UtcNow;

        if (referee.NotifyInApp)
            await AddNotificationAsync(referee.AccountId, $"Bạn được gán làm trọng tài trận {label} của giải {tournament.Name}.", now);

        var teamIds = new[] { match.TeamAid, match.TeamBid };
        var captains = await _uow.Repository<Team>().Query()
            .Where(t => teamIds.Contains(t.TeamId) && t.CaptainAccount.NotifyInApp)
            .Select(t => t.CaptainAccountId)
            .Distinct()
            .ToListAsync();

        foreach (var captainId in captains.Where(id => id != referee.AccountId))
            await AddNotificationAsync(captainId, $"Trận {label} của giải {tournament.Name} do {referee.FullName} làm trọng tài.", now);
    }

    private Task AddNotificationAsync(int accountId, string body, DateTime now)
        => _uow.Repository<Notification>().AddAsync(new Notification
        {
            AccountId = accountId,
            NotificationType = "SYSTEM",
            Title = "Phân công trọng tài",
            Body = body,
            Channel = "IN_APP",
            IsRead = false,
            SentAt = now
        });
}
