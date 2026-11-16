using System.Security.Cryptography;
using System.Text;
using ETOMS_API.DTOs.Bracket;
using ETOMS_API.Models;
using ETOMS_API.Repositories;
using ETOMS_API.Repositories.Interface;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Services.Impl;

// Module BRK-10, BRK-11 (UC-20). Dữ liệu nhánh knockout công khai cho trang sơ đồ cây và trang danh sách.
// Chỉ đọc. Cây dựng theo ParentNodeId như BRK-01/04 và BRK-08 sinh ra; trận tranh hạng Ba nhận ra bằng ThirdPlaceMatch.
// Nhánh chưa khóa thì chưa công khai: khách chỉ thấy danh sách đội đã duyệt (UC-20 E1), Ban tổ chức xem trước được.
public class BracketViewService : IBracketViewService
{
    private readonly IUnitOfWork _uow;

    public BracketViewService(IUnitOfWork uow) => _uow = uow;

    public async Task<BracketViewDto?> GetBracketAsync(int tournamentId, int? viewerAccountId)
    {
        var t = await _uow.Repository<Tournament>().GetByIdAsync(tournamentId);
        if (t is null) return null;
        var isOrganizer = viewerAccountId == t.OrganizerAccountId;
        if (t.Status == "DRAFT" && !isOrganizer) return null;

        var dto = new BracketViewDto
        {
            TournamentId = t.TournamentId,
            TournamentName = t.Name,
            FormatLabel = t.Format switch
            {
                "SINGLE_ELIMINATION" => "Loại trực tiếp",
                "DOUBLE_ELIMINATION" => "Loại kép",
                "ROUND_ROBIN" => "Vòng tròn",
                "HYBRID" => "Vòng bảng rồi loại trực tiếp",
                _ => t.Format
            },
            TournamentStatus = StatusText.Tournament(t.Status),
            IsLocked = t.BracketLockedAt is not null,
            IsOrganizer = isOrganizer,
            IsLive = t.Status is "PUBLISHED" or "ONGOING"
        };

        if (!dto.IsLocked && !isOrganizer)
        {
            dto.CanViewBracket = false;
            dto.Notice = "Nhánh đấu chưa khóa nên chưa công bố. Dưới đây là các đội đã được duyệt tham gia.";
            dto.Participants = await _uow.Repository<Registration>().Query()
                .Where(r => r.TournamentId == tournamentId && r.Status == "APPROVED")
                .Select(r => r.Team.Name)
                .OrderBy(n => n)
                .ToListAsync();
            dto.Version = Hash("participants|" + string.Join("|", dto.Participants));
            return dto;
        }

        dto.CanViewBracket = true;
        if (!dto.IsLocked) dto.Notice = "Bản xem trước cho Ban tổ chức: nhánh chưa khóa nên khách chưa xem được. Bấm \"Đổi chỗ đội\" để kéo thả sắp xếp lại, xong thì \"Khóa nhánh\" để công bố.";

        var nodes = await _uow.Repository<BracketNode>().Query().Where(n => n.TournamentId == tournamentId).ToListAsync();
        dto.HasGroupStage = nodes.Any(n => n.StageType == "GROUP");
        var knockout = nodes.Where(n => n.StageType == "KNOCKOUT").ToList();
        if (knockout.Count == 0)
        {
            dto.Version = Hash("nodes|" + nodes.Count);
            return dto;
        }

        var nodeIds = knockout.Select(n => n.BracketNodeId).ToList();
        var matches = await _uow.Repository<Match>().Query().Where(m => nodeIds.Contains(m.BracketNodeId)).ToListAsync();
        var matchByNode = matches.GroupBy(m => m.BracketNodeId).ToDictionary(g => g.Key, g => g.First());
        var matchIds = matches.Select(m => m.MatchId).ToList();
        var games = (await _uow.Repository<MatchGame>().Query().Where(g => matchIds.Contains(g.MatchId)).ToListAsync())
            .GroupBy(g => g.MatchId).ToDictionary(g => g.Key, g => g.OrderBy(x => x.GameNo).ToList());

        var teamIds = matches.SelectMany(m => new[] { m.TeamAid, m.TeamBid }).Concat(knockout.Select(n => n.SeedTeamId))
            .Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();
        var teams = await _uow.Repository<Team>().Query().Where(x => teamIds.Contains(x.TeamId)).ToDictionaryAsync(x => x.TeamId, x => x.Name);
        var refereeIds = matches.Where(m => m.RefereeAccountId.HasValue).Select(m => m.RefereeAccountId!.Value).Distinct().ToList();
        var referees = await _uow.Repository<Account>().Query().Where(a => refereeIds.Contains(a.AccountId)).ToDictionaryAsync(a => a.AccountId, a => a.FullName);

        var lastStage = knockout.Max(n => n.StageNo);
        var multiStage = dto.HasGroupStage || knockout.Select(n => n.StageNo).Distinct().Count() > 1;
        var version = new StringBuilder();

        foreach (var stage in knockout.GroupBy(n => n.StageNo).OrderBy(g => g.Key))
        {
            var stageNodes = stage.ToList();
            var finalRound = stageNodes.Max(n => n.RoundNo ?? 0);
            bool IsThird(BracketNode n) => ThirdPlaceMatch.IsNode(n.StageType, n.StageNo, n.RoundNo, n.PositionInRound, n.ParentNodeId, lastStage, finalRound);
            string Label(BracketNode n)
            {
                if (IsThird(n)) return ThirdPlaceMatch.Label;
                var round = BracketLabels.KnockoutRound(finalRound, n.RoundNo);
                return round == "Chung kết" ? round : $"{round} · Trận {n.PositionInRound}";
            }
            var final = stageNodes.FirstOrDefault(n => n.RoundNo == finalRound && n.PositionInRound == 1 && n.ParentNodeId == null);
            List<BracketNode> Children(BracketNode n)
            {
                var parentId = IsThird(n) ? final?.BracketNodeId : n.BracketNodeId;
                return parentId is null ? new List<BracketNode>() : stageNodes.Where(c => c.ParentNodeId == parentId).OrderBy(c => c.PositionInRound).ToList();
            }
            BracketSlotDto Slot(BracketNode n, int? teamId, int side)
            {
                if (teamId is int id) return new BracketSlotDto { TeamId = id, Name = teams.TryGetValue(id, out var nm) ? nm : "Đội #" + id };
                var child = Children(n).ElementAtOrDefault(side);
                if (child is null) return new BracketSlotDto { Name = "Chưa xác định" };
                if (child.IsBye && child.SeedTeamId is int seed && !IsThird(n))
                    return new BracketSlotDto { TeamId = seed, Name = teams.TryGetValue(seed, out var sn) ? sn : "Đội #" + seed };
                return new BracketSlotDto { Name = (IsThird(n) ? "Thua " : "Thắng ") + Label(child) };
            }

            var view = new BracketStageViewDto
            {
                StageNo = stage.Key,
                Label = multiStage ? $"Giai đoạn {stage.Key} · Knockout" : "Nhánh knockout",
                Rounds = stageNodes.Select(n => n.RoundNo ?? 0).Distinct().OrderBy(r => r)
                    .Select(r => new BracketRoundViewDto { RoundNo = r, Label = BracketLabels.KnockoutRound(finalRound, r) }).ToList()
            };

            foreach (var n in stageNodes.OrderBy(n => n.RoundNo).ThenBy(n => n.PositionInRound))
            {
                matchByNode.TryGetValue(n.BracketNodeId, out var m);
                var node = new BracketNodeViewDto
                {
                    NodeId = n.BracketNodeId,
                    ParentNodeId = n.ParentNodeId,
                    RoundNo = n.RoundNo ?? 0,
                    Position = n.PositionInRound ?? 0,
                    Label = Label(n),
                    IsBye = n.IsBye,
                    IsThirdPlace = IsThird(n),
                    MatchId = m?.MatchId,
                    TeamA = n.IsBye ? Slot(n, n.SeedTeamId, 0) : Slot(n, m?.TeamAid, 0),
                    TeamB = n.IsBye ? new BracketSlotDto { Name = "Miễn vòng 1" } : Slot(n, m?.TeamBid, 1),
                    ScoreA = m?.ScoreA,
                    ScoreB = m?.ScoreB,
                    WinnerTeamId = n.IsBye ? n.SeedTeamId : m?.WinnerTeamId,
                    Status = m?.Status,
                    StatusLabel = m is null ? null : StatusText.Match(m.Status),
                    StatusClass = m is null ? null : StatusText.MatchClass(m.Status),
                    ScheduledAt = m?.ScheduledAt,
                    BestOf = m?.BestOf ?? 0,
                    RefereeName = m?.RefereeAccountId is int r && referees.TryGetValue(r, out var rn) ? rn : null,
                    Games = m is not null && games.TryGetValue(m.MatchId, out var gs)
                        ? gs.Select(g => new BracketGameDto { GameNo = g.GameNo, ScoreA = g.ScoreA, ScoreB = g.ScoreB }).ToList()
                        : new List<BracketGameDto>()
                };
                view.Nodes.Add(node);
                version.Append($"{node.NodeId}:{node.TeamA.TeamId}:{node.TeamB.TeamId}:{node.ScoreA}:{node.ScoreB}:{node.WinnerTeamId}:{node.Status}:{node.ScheduledAt:O}:{node.RefereeName}:{node.Games.Count};");
            }
            dto.Stages.Add(view);
        }

        dto.Version = Hash(version.ToString());
        return dto;
    }

    private static string Hash(string text)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16];
}
