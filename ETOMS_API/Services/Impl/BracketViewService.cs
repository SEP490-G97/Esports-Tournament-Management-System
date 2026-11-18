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
// Nhánh đã sinh thì ai cũng xem được (chưa khóa = bản tạm, chỉ đọc); chưa sinh nhánh thì người xem thấy danh sách đội đã duyệt.
// Lưu ý: nới so với UC-20 E1 (trước đây nhánh chưa khóa chỉ BTC xem) theo yêu cầu 15/11/2026.
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

        var nodes = await _uow.Repository<BracketNode>().Query().Where(n => n.TournamentId == tournamentId).ToListAsync();

        // Chưa sinh nhánh: người xem (không phải BTC) thấy danh sách đội đã duyệt thay vì trang trống.
        if (nodes.Count == 0 && !isOrganizer)
        {
            dto.CanViewBracket = false;
            dto.Notice = DateTime.UtcNow < t.RegCloseAt
                ? $"Giải đang nhận đăng ký đến {t.RegCloseAt.AddHours(7):HH:mm dd/MM/yyyy}. Nhánh đấu được sinh sau khi đóng đăng ký — dưới đây là các đội đã được duyệt."
                : "Ban tổ chức chưa sinh nhánh đấu. Dưới đây là các đội đã được duyệt tham gia.";
            dto.Participants = await _uow.Repository<Registration>().Query()
                .Where(r => r.TournamentId == tournamentId && r.Status == "APPROVED")
                .Select(r => r.Team.Name)
                .OrderBy(n => n)
                .ToListAsync();
            dto.Version = Hash("participants|" + string.Join("|", dto.Participants));
            return dto;
        }

        // Nhánh đã sinh thì MỌI vai trò đều xem cùng một sơ đồ; chỉ BTC của giải có nút đổi chỗ/khóa
        // (API swap/lock/unlock tự kiểm OrganizerAccountId), người khác chỉ đọc.
        dto.CanViewBracket = true;
        if (!dto.IsLocked)
            dto.Notice = isOrganizer
                ? "Nhánh chưa khóa: mọi người đang xem bản tạm này (chỉ đọc). Bấm \"Đổi chỗ đội\" để kéo thả sắp xếp lại, xong thì \"Khóa nhánh\" để chốt."
                : "Nhánh đấu tạm thời: Ban tổ chức chưa khóa nên các cặp đấu còn có thể thay đổi.";

        dto.HasGroupStage = nodes.Any(n => n.StageType == "GROUP");
        var knockout = nodes.Where(n => n.StageType == "KNOCKOUT").ToList();
        if (knockout.Count == 0)
        {
            // Giải vòng bảng rồi loại trực tiếp đang ở vòng bảng: vẽ trước sơ đồ knockout DỰ KIẾN theo đúng
            // cách BRK-08 sẽ xếp (KnockoutDraw: Nhất bảng A gặp Nhì bảng B...), chỗ đội là "Nhất bảng A"...
            var projected = t.Format == "HYBRID" ? await ProjectKnockoutAsync(t, nodes) : null;
            if (projected is not null) dto.Stages.Add(projected);
            dto.Version = Hash("nodes|" + nodes.Count + "|" + (projected?.Nodes.Count ?? 0));
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
        // Loại kép: 3 khối Nhánh thắng / Nhánh thua / Chung kết tổng (StageNo 1/2/3), nhãn và ô chờ theo DoubleElimination.
        var de = DoubleElimination.IsDoubleElimination(t.Format) ? new DoubleElimination.Layout(knockout) : null;

        foreach (var stage in knockout.GroupBy(n => n.StageNo).OrderBy(g => g.Key))
        {
            var stageNodes = stage.ToList();
            var finalRound = stageNodes.Max(n => n.RoundNo ?? 0);
            bool IsThird(BracketNode n) => de is null && ThirdPlaceMatch.IsNode(n.StageType, n.StageNo, n.RoundNo, n.PositionInRound, n.ParentNodeId, lastStage, finalRound);
            string Label(BracketNode n)
            {
                if (de is not null) return de.ShortMatchLabel(n);
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
                if (de is not null)
                {
                    var feeder = de.Feeder(n, side == 0);
                    if (feeder is not { } f) return new BracketSlotDto { Name = "Chưa xác định" };
                    if (!f.Loser && f.From.IsBye && f.From.SeedTeamId is int byeTeam)
                        return new BracketSlotDto { TeamId = byeTeam, Name = teams.TryGetValue(byeTeam, out var bn) ? bn : "Đội #" + byeTeam };
                    // Cùng khối thì nhãn ngắn ("Thắng Bán kết · Trận 1"), khác khối ghi đủ tên nhánh.
                    var from = f.From.StageNo == n.StageNo ? de.ShortMatchLabel(f.From) : de.MatchLabel(f.From);
                    return new BracketSlotDto { Name = (f.Loser ? "Thua " : "Thắng ") + from };
                }
                var child = Children(n).ElementAtOrDefault(side);
                if (child is null) return new BracketSlotDto { Name = "Chưa xác định" };
                if (child.IsBye && child.SeedTeamId is int seed && !IsThird(n))
                    return new BracketSlotDto { TeamId = seed, Name = teams.TryGetValue(seed, out var sn) ? sn : "Đội #" + seed };
                return new BracketSlotDto { Name = (IsThird(n) ? "Thua " : "Thắng ") + Label(child) };
            }

            var view = new BracketStageViewDto
            {
                StageNo = stage.Key,
                Label = de is not null ? DoubleElimination.StageLabel(stage.Key)
                    : multiStage ? $"Giai đoạn {stage.Key} · Knockout" : "Nhánh knockout",
                Rounds = stageNodes.Select(n => n.RoundNo ?? 0).Distinct().OrderBy(r => r)
                    .Select(r => new BracketRoundViewDto { RoundNo = r, Label = de?.ShortRoundLabel(stage.Key, r) ?? BracketLabels.KnockoutRound(finalRound, r) }).ToList()
            };

            foreach (var n in stageNodes.OrderBy(n => n.RoundNo).ThenBy(n => n.PositionInRound))
            {
                matchByNode.TryGetValue(n.BracketNodeId, out var m);
                var node = new BracketNodeViewDto
                {
                    NodeId = n.BracketNodeId,
                    // Loại kép: đường nối sang khối khác (chung kết nhánh thắng/thua -> chung kết tổng) không vẽ,
                    // ô chờ đã ghi "Thắng Chung kết nhánh thắng"...
                    ParentNodeId = de is not null && !stageNodes.Any(x => x.BracketNodeId == n.ParentNodeId) ? null : n.ParentNodeId,
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
                version.Append($"{node.NodeId}:{node.TeamA.TeamId}:{node.TeamB.TeamId}:{node.ScoreA}:{node.ScoreB}:{node.WinnerTeamId}:{node.Status}:{node.ScheduledAt:O}:{node.RefereeName}:{string.Join(",", node.Games.Select(g => $"{g.ScoreA}-{g.ScoreB}"))};");
            }
            dto.Stages.Add(view);
        }

        dto.Version = Hash(version.ToString());
        return dto;
    }

    // Sơ đồ knockout dự kiến (chỉ để xem, không lưu, không có trận): số đội đi tiếp mỗi bảng =
    // min(AdvancePerGroup, số đội của bảng). Id node là số âm để không trùng node thật.
    private async Task<BracketStageViewDto?> ProjectKnockoutAsync(Tournament t, List<BracketNode> nodes)
    {
        var advance = t.AdvancePerGroup ?? 0;
        if (advance < 1) return null;
        var groupNodes = nodes.Where(n => n.StageType == "GROUP" && !string.IsNullOrWhiteSpace(n.GroupLabel)).ToList();
        if (groupNodes.Count == 0) return null;

        var groupOf = groupNodes.ToDictionary(n => n.BracketNodeId, n => n.GroupLabel!.Trim());
        var groupNodeIds = groupOf.Keys.ToList();
        var pairs = await _uow.Repository<Match>().Query()
            .Where(m => groupNodeIds.Contains(m.BracketNodeId))
            .Select(m => new { m.BracketNodeId, m.TeamAid, m.TeamBid })
            .ToListAsync();
        var teamsPerGroup = groupNodes
            .GroupBy(n => groupOf[n.BracketNodeId])
            .ToDictionary(g => g.Key, g => g.Select(n => n.SeedTeamId)
                .Concat(pairs.Where(p => groupOf[p.BracketNodeId] == g.Key).SelectMany(p => new[] { p.TeamAid, p.TeamBid }))
                .Where(x => x.HasValue).Select(x => x!.Value).Distinct().Count());

        var qualifiers = teamsPerGroup.OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => (IReadOnlyList<KnockoutDraw.Qualifier>)Enumerable.Range(1, Math.Min(advance, g.Value))
                .Select(pos => new KnockoutDraw.Qualifier(0, g.Key, pos)).ToList())
            .Where(q => q.Count > 0)
            .ToList();
        if (qualifiers.Sum(q => q.Count) < 2) return null;

        var slots = KnockoutDraw.Build(qualifiers);
        var size = slots.Count;
        var totalRounds = (int)Math.Log2(size);
        var view = new BracketStageViewDto
        {
            StageNo = (nodes.Max(n => (int?)n.StageNo) ?? 1) + 1,
            Label = "Nhánh knockout dự kiến (xếp theo thứ hạng vòng bảng)",
            IsProjected = true,
            Rounds = Enumerable.Range(1, totalRounds)
                .Select(r => new BracketRoundViewDto { RoundNo = r, Label = BracketLabels.KnockoutRound(totalRounds, r) }).ToList()
        };

        // Id âm: vòng r, vị trí p -> -(r * 1000 + p); cha của (r, p) là (r + 1, (p + 1) / 2).
        static int Id(int round, int pos) => -(round * 1000 + pos);
        string Label(int round, int pos)
        {
            var name = BracketLabels.KnockoutRound(totalRounds, round);
            return name == "Chung kết" ? name : $"{name} · Trận {pos}";
        }
        var byes = new HashSet<int>();
        for (var round = 1; round <= totalRounds; round++)
        {
            var count = size >> round;
            for (var pos = 1; pos <= count; pos++)
            {
                BracketSlotDto From(int side)
                {
                    if (round == 1)
                    {
                        var s = slots[2 * (pos - 1) + side];
                        return new BracketSlotDto { Name = s?.Team.Label ?? "Miễn vòng 1" };
                    }
                    var childPos = 2 * (pos - 1) + side + 1;
                    if (byes.Contains(Id(round - 1, childPos)))
                    {
                        var s = slots[2 * (childPos - 1)] ?? slots[2 * (childPos - 1) + 1];
                        return new BracketSlotDto { Name = s!.Team.Label };
                    }
                    return new BracketSlotDto { Name = "Thắng " + Label(round - 1, childPos) };
                }

                var isBye = round == 1 && (slots[2 * (pos - 1)] is null || slots[2 * (pos - 1) + 1] is null);
                if (isBye) byes.Add(Id(round, pos));
                var a = From(0);
                var b = From(1);
                view.Nodes.Add(new BracketNodeViewDto
                {
                    NodeId = Id(round, pos),
                    ParentNodeId = round < totalRounds ? Id(round + 1, (pos + 1) / 2) : null,
                    RoundNo = round,
                    Position = pos,
                    Label = Label(round, pos),
                    IsBye = isBye,
                    TeamA = isBye ? new BracketSlotDto { Name = a.Name == "Miễn vòng 1" ? b.Name : a.Name } : a,
                    TeamB = isBye ? new BracketSlotDto { Name = "Miễn vòng 1" } : b,
                    BestOf = StageTransitionService.BestOfFor(t.BestOfPerRound, round, totalRounds),
                    StatusLabel = "Dự kiến"
                });
            }
        }
        return view;
    }

    private static string Hash(string text)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16];
}
