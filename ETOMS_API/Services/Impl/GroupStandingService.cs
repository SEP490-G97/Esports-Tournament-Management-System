using ETOMS_API.DTOs.Bracket;
using ETOMS_API.Models;
using ETOMS_API.Repositories;
using ETOMS_API.Repositories.Interface;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Services.Impl;

// Module BRK-13 (UC-18 bước 4). Bảng xếp hạng vòng bảng công khai cho mọi người xem.
// Tính bằng GroupStandingCalculator như BRK-08 nên thứ hạng ở đây và ở trang chuyển giai đoạn luôn khớp.
// Giải HYBRID đánh dấu suất đi tiếp: tạm thời khi vòng bảng còn đấu, chính thức khi đã xong,
// và lấy đúng danh sách đội đã vào nhánh knockout sau khi chuyển giai đoạn (kể cả đội do Ban tổ chức xếp khi hòa).
public class GroupStandingService : IGroupStandingService
{
    private readonly IUnitOfWork _uow;

    public GroupStandingService(IUnitOfWork uow) => _uow = uow;

    public async Task<GroupStandingBoardDto?> GetStandingsAsync(int tournamentId, int? viewerAccountId)
    {
        var t = await _uow.Repository<Tournament>().GetByIdAsync(tournamentId);
        if (t is null) return null;
        if (t.Status == "DRAFT" && t.OrganizerAccountId != viewerAccountId) return null;

        var order = GroupStandingCalculator.ParseOrder(t.TieBreakerOrder);
        var board = new GroupStandingBoardDto
        {
            TournamentId = t.TournamentId,
            TournamentName = t.Name,
            FormatLabel = t.Format switch
            {
                "ROUND_ROBIN" => "Vòng tròn",
                "HYBRID" => "Vòng bảng rồi loại trực tiếp",
                "SINGLE_ELIMINATION" => "Loại trực tiếp",
                "DOUBLE_ELIMINATION" => "Loại kép",
                _ => t.Format
            },
            AdvancePerGroup = t.Format == "HYBRID" ? t.AdvancePerGroup : null,
            IsLocked = t.BracketLockedAt is not null,
            TieBreakerOrder = order
        };

        if (t.Format is not ("ROUND_ROBIN" or "HYBRID"))
            return State(board, "NOT_GROUP_FORMAT", "Giải đấu theo thể thức loại trực tiếp, không có vòng bảng.");

        var nodes = await _uow.Repository<BracketNode>().Query()
            .Where(n => n.TournamentId == tournamentId)
            .Select(n => new { n.BracketNodeId, n.StageNo, n.StageType, n.GroupLabel, n.RoundNo, n.IsBye, n.SeedTeamId })
            .ToListAsync();
        var groupNodes = nodes.Where(n => n.StageType == "GROUP").ToList();
        if (groupNodes.Count == 0)
            return State(board, "NO_GROUPS", "Giải chưa chia bảng.");

        var groupStage = groupNodes.Max(n => n.StageNo);
        var nodeInfo = groupNodes.ToDictionary(n => n.BracketNodeId);
        var nodeIds = nodeInfo.Keys.ToList();
        var matches = await _uow.Repository<Match>().Query()
            .Where(m => nodeIds.Contains(m.BracketNodeId))
            .Select(m => new { m.MatchId, m.BracketNodeId, m.TeamAid, m.TeamBid, m.ScoreA, m.ScoreB, m.WinnerTeamId, m.Status, m.ScheduledAt })
            .ToListAsync();

        // Đội đã vào knockout (sau BRK-08): đội có trong trận vòng 1 hoặc được miễn vòng 1.
        var knockoutRoundOne = nodes.Where(n => n.StageType == "KNOCKOUT" && n.StageNo > groupStage && n.RoundNo == 1).ToList();
        var advanced = knockoutRoundOne.Where(n => n.IsBye && n.SeedTeamId.HasValue).Select(n => n.SeedTeamId!.Value).ToHashSet();
        if (knockoutRoundOne.Count > 0)
        {
            var koIds = knockoutRoundOne.Select(n => n.BracketNodeId).ToList();
            foreach (var m in await _uow.Repository<Match>().Query().Where(m => koIds.Contains(m.BracketNodeId)).Select(m => new { m.TeamAid, m.TeamBid }).ToListAsync())
            {
                if (m.TeamAid is int a) advanced.Add(a);
                if (m.TeamBid is int b) advanced.Add(b);
            }
        }

        var teamIds = matches.SelectMany(m => new[] { m.TeamAid, m.TeamBid }).Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();
        var names = await _uow.Repository<Team>().Query().Where(x => teamIds.Contains(x.TeamId)).ToDictionaryAsync(x => x.TeamId, x => x.Name);
        string Name(int? id) => id is int v && names.TryGetValue(v, out var n) ? n : "Chưa xác định";

        GroupStandingCalculator.GroupMatch Calc(int? a, int? b, int? sa, int? sb, int? w, string s) => new(a, b, sa, sb, w, s);
        board.TotalMatches = matches.Count;
        board.FinishedMatches = matches.Count(m => GroupStandingCalculator.IsFinished(Calc(m.TeamAid, m.TeamBid, m.ScoreA, m.ScoreB, m.WinnerTeamId, m.Status)));
        var finished = board.FinishedMatches == board.TotalMatches;
        var isAdvanced = knockoutRoundOne.Count > 0;
        var advance = board.AdvancePerGroup;

        foreach (var group in matches.GroupBy(m => nodeInfo[m.BracketNodeId].GroupLabel ?? "?").OrderBy(g => g.Key))
        {
            var rows = GroupStandingCalculator.Compute(group.Select(m => Calc(m.TeamAid, m.TeamBid, m.ScoreA, m.ScoreB, m.WinnerTeamId, m.Status)), order, null, group.Key + "-");
            var blockingTies = rows.Where(r => r.TieKey is not null && advance is int k && rows.Where(x => x.TieKey == r.TieKey).Min(x => x.Position) <= k)
                .Select(r => r.TeamId).ToHashSet();

            board.Groups.Add(new StandingGroupDto
            {
                GroupLabel = group.Key,
                Rows = rows.Select(r => new StandingRowDto
                {
                    Position = r.Position,
                    TeamId = r.TeamId,
                    TeamName = Name(r.TeamId),
                    Played = r.Played,
                    Won = r.Won,
                    Lost = r.Lost,
                    GamesWon = r.GamesWon,
                    GamesLost = r.GamesLost,
                    Diff = r.Diff,
                    Points = r.Points,
                    DecidedBy = r.DecidedBy,
                    IsTied = r.TieKey is not null,
                    Mark = advance is not int k ? null
                        : isAdvanced ? (advanced.Contains(r.TeamId) ? "QUALIFIED" : "OUT")
                        : blockingTies.Contains(r.TeamId) ? (finished ? "PENDING_DECISION" : "PROVISIONAL")
                        : r.Position <= k ? (finished ? "QUALIFIED" : "PROVISIONAL")
                        : finished ? "OUT" : null
                }).ToList(),
                Matches = group.OrderBy(m => nodeInfo[m.BracketNodeId].RoundNo).ThenBy(m => m.MatchId).Select(m => new StandingMatchDto
                {
                    MatchId = m.MatchId,
                    RoundNo = nodeInfo[m.BracketNodeId].RoundNo ?? 0,
                    TeamAName = Name(m.TeamAid),
                    TeamBName = Name(m.TeamBid),
                    ScoreA = m.ScoreA,
                    ScoreB = m.ScoreB,
                    IsDecided = GroupStandingCalculator.IsDecided(Calc(m.TeamAid, m.TeamBid, m.ScoreA, m.ScoreB, m.WinnerTeamId, m.Status)),
                    StatusLabel = StatusText.Match(m.Status),
                    StatusClass = StatusText.MatchClass(m.Status),
                    ScheduledAt = m.ScheduledAt
                }).ToList()
            });
        }

        if (isAdvanced) return State(board, "ADVANCED", "Vòng bảng đã kết thúc, các đội đi tiếp đã vào nhánh knockout.");
        if (finished)
            return State(board, "FINISHED", board.Groups.Any(g => g.Rows.Any(r => r.Mark == "PENDING_DECISION"))
                ? "Vòng bảng đã kết thúc. Có đội bằng nhau sau mọi tiêu chí, chờ Ban tổ chức xếp hạng."
                : "Vòng bảng đã kết thúc.");
        return State(board, "IN_PROGRESS", $"Vòng bảng đang diễn ra: {board.FinishedMatches}/{board.TotalMatches} trận đã có kết quả.");
    }

    private static GroupStandingBoardDto State(GroupStandingBoardDto board, string state, string message)
    {
        board.State = state;
        board.StateMessage = message;
        return board;
    }
}
