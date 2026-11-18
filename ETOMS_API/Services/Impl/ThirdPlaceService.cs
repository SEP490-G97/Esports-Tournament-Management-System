using ETOMS_API.DTOs.Bracket;
using ETOMS_API.Models;
using ETOMS_API.Repositories;
using ETOMS_API.Repositories.Interface;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Services.Impl;

// Module BRK-09. Giải bật Tournament.HasThirdPlaceMatch: tạo trận tranh hạng Ba cho hai đội thua bán kết.
// Trận nằm ở cùng giai đoạn và cùng vòng với chung kết, vị trí 2, không có node cha
// (ThirdPlaceMatch.IsNode). Đội thắng trận này là hạng Ba; ghi hạng chung cuộc thuộc UC-19.
// Đội thua bán kết vị trí lẻ vào ô A, vị trí chẵn vào ô B, giống quy tắc đẩy đội thắng của ResultService.
// Trận đã bắt đầu thì không tự đổi đội nữa; kết quả bán kết đổi sau đó là việc của Ban tổ chức.
public class ThirdPlaceService : IThirdPlaceService
{
    private readonly IUnitOfWork _uow;

    public ThirdPlaceService(IUnitOfWork uow) => _uow = uow;

    private sealed class Plan
    {
        public Tournament Tournament { get; init; } = null!;
        public string State { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public BracketNode? Final { get; set; }
        public BracketNode? ThirdNode { get; set; }
        public Match? ThirdMatch { get; set; }
        public int?[] Losers { get; } = new int?[2];
        public int BestOf { get; set; } = 1;
    }

    public async Task<ThirdPlaceStatusDto?> GetStatusAsync(int tournamentId)
    {
        var tournament = await _uow.Repository<Tournament>().GetByIdAsync(tournamentId);
        if (tournament is null) return null;
        return await ToDtoAsync(await InspectAsync(tournament));
    }

    // ===== BRK-09: Trận tranh hạng Ba =====
    public async Task<ThirdPlaceStatusDto?> SyncAsync(int tournamentId)
    {
        var tournament = await _uow.Repository<Tournament>().GetByIdAsync(tournamentId);
        if (tournament is null) return null;

        var plan = await InspectAsync(tournament);
        await ApplyAsync(plan);
        return await ToDtoAsync(await InspectAsync(tournament));
    }

    public async Task<int> RunScheduledAsync(CancellationToken cancellationToken)
    {
        var ids = await _uow.Repository<Tournament>().Query()
            .Where(t => t.HasThirdPlaceMatch && t.BracketLockedAt != null && t.Status != "COMPLETED" && t.Status != "CANCELLED"
                && (t.Format == "SINGLE_ELIMINATION" || t.Format == "HYBRID"))
            .Select(t => t.TournamentId)
            .ToListAsync(cancellationToken);

        var changed = 0;
        foreach (var id in ids)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var tournament = await _uow.Repository<Tournament>().GetByIdAsync(id);
            if (tournament is null) continue;
            if (await ApplyAsync(await InspectAsync(tournament))) changed++;
        }
        return changed;
    }

    private async Task<Plan> InspectAsync(Tournament t)
    {
        var plan = new Plan { Tournament = t };
        if (!t.HasThirdPlaceMatch) return Stop(plan, "DISABLED", "Giải không bật trận tranh hạng Ba.");
        if (t.Format is not ("SINGLE_ELIMINATION" or "HYBRID"))
            return Stop(plan, "NOT_APPLICABLE", "Thể thức của giải không có trận tranh hạng Ba.");
        if (t.BracketLockedAt is null) return Stop(plan, "NOT_LOCKED", "Nhánh đấu chưa khóa.");

        var nodes = await _uow.Repository<BracketNode>().Query()
            .Where(n => n.TournamentId == t.TournamentId && n.StageType == "KNOCKOUT")
            .ToListAsync();
        if (nodes.Count == 0) return Stop(plan, "NO_SEMIFINALS", "Giải chưa có nhánh knockout.");

        var stage = nodes.Max(n => n.StageNo);
        var finalRound = nodes.Where(n => n.StageNo == stage).Max(n => n.RoundNo ?? 0);
        plan.Final = nodes.FirstOrDefault(n => n.StageNo == stage && n.RoundNo == finalRound && n.PositionInRound == 1 && n.ParentNodeId == null);
        var semis = plan.Final is null ? new List<BracketNode>()
            : nodes.Where(n => n.ParentNodeId == plan.Final.BracketNodeId).OrderBy(n => n.PositionInRound).ToList();
        if (semis.Count != 2 || semis.Any(n => n.IsBye))
            return Stop(plan, "NO_SEMIFINALS", "Nhánh đấu không có đủ hai trận bán kết nên không có trận tranh hạng Ba.");

        plan.ThirdNode = nodes.FirstOrDefault(n => ThirdPlaceMatch.IsNode(n.StageType, n.StageNo, n.RoundNo, n.PositionInRound, n.ParentNodeId, stage, finalRound));
        var semiIds = semis.Select(n => n.BracketNodeId).ToList();
        var semiMatches = await _uow.Repository<Match>().Query().Where(m => semiIds.Contains(m.BracketNodeId)).ToListAsync();
        plan.BestOf = semiMatches.Count == 0 ? 1 : semiMatches.Max(m => m.BestOf);
        for (var i = 0; i < 2; i++)
        {
            var m = semiMatches.FirstOrDefault(x => x.BracketNodeId == semis[i].BracketNodeId);
            if (m is { TeamAid: not null, TeamBid: not null, WinnerTeamId: not null } && m.Status is "VERIFIED" or "WALKOVER")
                plan.Losers[i] = m.WinnerTeamId == m.TeamAid ? m.TeamBid : m.TeamAid;
        }

        if (plan.ThirdNode is not null)
            plan.ThirdMatch = await _uow.Repository<Match>().Query().FirstOrDefaultAsync(m => m.BracketNodeId == plan.ThirdNode.BracketNodeId);

        var third = plan.ThirdMatch;
        if (third is not null && third.WinnerTeamId is not null && third.Status is "VERIFIED" or "WALKOVER")
            return Stop(plan, "PLAYED", "Trận tranh hạng Ba đã có kết quả.");
        if (third is not null && !CanRefill(third) && Differs(third, plan.Losers))
            return Stop(plan, "CONFLICT", "Kết quả bán kết đã đổi sau khi trận tranh hạng Ba bắt đầu. Ban tổ chức cần kiểm tra lại hai đội của trận này.");

        var a = third is null ? plan.Losers[0] : plan.Losers[0] ?? third.TeamAid;
        var b = third is null ? plan.Losers[1] : plan.Losers[1] ?? third.TeamBid;
        return a is not null && b is not null
            ? Stop(plan, "READY", "Đã đủ hai đội thua bán kết, trận tranh hạng Ba chờ thi đấu.")
            : Stop(plan, "WAITING", "Trận tranh hạng Ba chờ kết quả bán kết.");
    }

    // Trả về true khi có ghi vào database.
    private async Task<bool> ApplyAsync(Plan plan)
    {
        if (plan.State is not ("WAITING" or "READY")) return false;
        var t = plan.Tournament;
        var now = DateTime.UtcNow;
        var placed = new List<int>();

        var match = plan.ThirdMatch;
        if (match is null)
        {
            var node = new BracketNode
            {
                TournamentId = t.TournamentId,
                StageNo = plan.Final!.StageNo,
                StageType = "KNOCKOUT",
                RoundNo = plan.Final.RoundNo,
                PositionInRound = ThirdPlaceMatch.Position,
                NodeType = "MATCH_SLOT",
                SeedingMethod = plan.Final.SeedingMethod,
                SeedingRandomSeed = plan.Final.SeedingRandomSeed,
                Status = "LOCKED",
                GeneratedAt = now
            };
            match = new Match
            {
                TournamentId = t.TournamentId,
                BracketNode = node,
                BestOf = plan.BestOf,
                CheckInMode = "SELF",
                Status = "PENDING"
            };
            await _uow.Repository<BracketNode>().AddAsync(node);
            await _uow.Repository<Match>().AddAsync(match);
        }

        if (CanRefill(match))
        {
            if (plan.Losers[0] is int la && match.TeamAid != la) { match.TeamAid = la; placed.Add(la); }
            if (plan.Losers[1] is int lb && match.TeamBid != lb) { match.TeamBid = lb; placed.Add(lb); }
        }

        if (plan.ThirdMatch is not null && placed.Count == 0) return false;

        if (placed.Count > 0)
        {
            var captains = await _uow.Repository<Team>().Query()
                .Where(x => placed.Contains(x.TeamId) && x.CaptainAccount.NotifyInApp)
                .Select(x => new { x.Name, x.CaptainAccountId })
                .ToListAsync();
            foreach (var c in captains)
                await _uow.Repository<Notification>().AddAsync(new Notification
                {
                    AccountId = c.CaptainAccountId,
                    NotificationType = "SYSTEM",
                    Title = "Trận tranh hạng Ba",
                    Body = $"Đội {c.Name} vào trận tranh hạng Ba của giải {t.Name}.",
                    Channel = "IN_APP",
                    IsRead = false,
                    SentAt = now
                });
        }

        await _uow.SaveChangesAsync();
        return true;
    }

    // Chỉ tự đổi đội khi trận chưa bắt đầu và chưa có tỷ số.
    private static bool CanRefill(Match m) => m.Status is "PENDING" or "READY" && m.ScoreA is null && m.ScoreB is null && m.WinnerTeamId is null;

    private static bool Differs(Match m, int?[] losers)
        => (losers[0] is int a && m.TeamAid != a) || (losers[1] is int b && m.TeamBid != b);

    private static Plan Stop(Plan plan, string state, string message)
    {
        plan.State = state;
        plan.Message = message;
        return plan;
    }

    private async Task<ThirdPlaceStatusDto> ToDtoAsync(Plan plan)
    {
        var dto = new ThirdPlaceStatusDto
        {
            TournamentId = plan.Tournament.TournamentId,
            Enabled = plan.Tournament.HasThirdPlaceMatch,
            State = plan.State,
            Message = plan.Message
        };
        var m = plan.ThirdMatch;
        if (m is null) return dto;

        var ids = new[] { m.TeamAid, m.TeamBid, m.WinnerTeamId }.Where(x => x.HasValue).Select(x => x!.Value).ToList();
        var names = await _uow.Repository<Team>().Query().Where(x => ids.Contains(x.TeamId)).ToDictionaryAsync(x => x.TeamId, x => x.Name);
        string? N(int? id) => id.HasValue && names.TryGetValue(id.Value, out var n) ? n : null;
        dto.MatchId = m.MatchId;
        dto.TeamAName = N(m.TeamAid);
        dto.TeamBName = N(m.TeamBid);
        dto.StatusLabel = StatusText.Match(m.Status);
        dto.WinnerName = N(m.WinnerTeamId);
        return dto;
    }
}

// Cách nhận ra trận tranh hạng Ba trong BracketNode, dùng chung cho các trang hiển thị và cho UC-19.
public static class ThirdPlaceMatch
{
    public const int Position = 2;
    public const string Label = "Tranh hạng Ba";

    public static bool IsNode(string stageType, int stageNo, int? roundNo, int? position, int? parentNodeId, int lastKnockoutStage, int finalRound)
        => stageType == "KNOCKOUT" && stageNo == lastKnockoutStage && roundNo == finalRound && position == Position && parentNodeId is null;
}
