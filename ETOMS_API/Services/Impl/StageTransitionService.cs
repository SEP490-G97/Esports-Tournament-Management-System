using ETOMS_API.DTOs.Bracket;
using ETOMS_API.Models;
using ETOMS_API.Repositories;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Services.Impl;

// Module BRK-08 (UC-18 A1). Giải HYBRID: khi mọi trận vòng bảng đã có kết quả, lấy Top N mỗi bảng
// (Tournament.AdvancePerGroup), xếp chéo và sinh cây knockout ở giai đoạn kế tiếp.
// Vòng bảng do BRK-01/06 sinh: mỗi BracketNode là một trận, StageType = GROUP, GroupLabel = A, B, ...
// Cây mới cùng kiểu với BRK-04: node vị trí lẻ đưa đội thắng vào ô A của trận cha, vị trí chẵn vào ô B
// (ResultService đẩy đội thắng theo đúng quy tắc này).
// Chạy tự động bằng StageTransitionWorker; Ban tổ chức bấm tay khi có đội hòa cần xếp (UC-18 A2).
// Giải bật tranh hạng Ba thì tạo luôn trận đó sau khi có cây (BRK-09).
public class StageTransitionService : IStageTransitionService
{
    private readonly IUnitOfWork _uow;
    private readonly IThirdPlaceService _thirdPlace;

    public StageTransitionService(IUnitOfWork uow, IThirdPlaceService thirdPlace)
    {
        _uow = uow;
        _thirdPlace = thirdPlace;
    }

    private sealed class Analysis
    {
        public Tournament Tournament { get; init; } = null!;
        public string State { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public int GroupStageNo { get; set; }
        public int Finished { get; set; }
        public int Total { get; set; }
        public List<string> Order { get; set; } = new();
        public List<GroupTableDto> Groups { get; } = new();
        public List<KnockoutDraw.Slot?> Slots { get; set; } = new();
        public List<KnockoutPairDto> Pairings { get; set; } = new();
        public Dictionary<int, string> TeamNames { get; set; } = new();
    }

    public async Task<int?> GetOrganizerAccountIdAsync(int tournamentId)
        => (await _uow.Repository<Tournament>().GetByIdAsync(tournamentId))?.OrganizerAccountId;

    public async Task<Result<StageTransitionBoardDto>> GetBoardAsync(int tournamentId, AdvanceStageRequest? decisions = null)
    {
        var tournament = await _uow.Repository<Tournament>().GetByIdAsync(tournamentId);
        if (tournament is null) return Result<StageTransitionBoardDto>.Failure("Không tìm thấy giải đấu.");

        var invalid = InvalidDecision(await AnalyzeAsync(tournament, null), decisions);
        if (invalid is not null) return Result<StageTransitionBoardDto>.Failure(invalid);
        return Result<StageTransitionBoardDto>.Success(ToDto(await AnalyzeAsync(tournament, decisions)));
    }

    // ===== BRK-08: Chuyển giai đoạn Bảng sang Knockout =====
    public async Task<Result<StageTransitionBoardDto>> AdvanceAsync(int tournamentId, AdvanceStageRequest request)
    {
        var tournament = await _uow.Repository<Tournament>().GetByIdAsync(tournamentId);
        if (tournament is null) return Result<StageTransitionBoardDto>.Failure("Không tìm thấy giải đấu.");

        var invalid = InvalidDecision(await AnalyzeAsync(tournament, null), request);
        if (invalid is not null) return Result<StageTransitionBoardDto>.Failure(invalid);
        var analysis = await AnalyzeAsync(tournament, request);

        // UC-18 E1: đã tạo ở lần chạy trước thì không làm gì.
        if (analysis.State == "DONE")
            return Result<StageTransitionBoardDto>.Success(ToDto(analysis), "Nhánh knockout đã được tạo trước đó, hệ thống không tạo lại.");
        if (analysis.State != "READY")
            return Result<StageTransitionBoardDto>.Failure(analysis.Message);

        var teamCount = await CreateKnockoutAsync(analysis);
        var after = await AnalyzeAsync(tournament, null);
        return Result<StageTransitionBoardDto>.Success(ToDto(after), $"Đã tạo nhánh knockout với {teamCount} đội đi tiếp. Hãy xếp lịch các trận.");
    }

    public async Task<int> RunScheduledAsync(CancellationToken cancellationToken)
    {
        var ids = await _uow.Repository<Tournament>().Query()
            .Where(t => t.Format == "HYBRID" && t.BracketLockedAt != null && t.Status != "COMPLETED" && t.Status != "CANCELLED")
            .Select(t => t.TournamentId)
            .ToListAsync(cancellationToken);

        var advanced = 0;
        foreach (var id in ids)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var tournament = await _uow.Repository<Tournament>().GetByIdAsync(id);
            if (tournament is null) continue;
            var analysis = await AnalyzeAsync(tournament, null);
            if (analysis.State != "READY") continue;
            await CreateKnockoutAsync(analysis);
            advanced++;
        }
        return advanced;
    }

    private async Task<Analysis> AnalyzeAsync(Tournament t, AdvanceStageRequest? decisions)
    {
        var a = new Analysis { Tournament = t, Order = GroupStandingCalculator.ParseOrder(t.TieBreakerOrder) };

        if (t.Format != "HYBRID")
            return Stop(a, "NOT_HYBRID", "Chỉ giải thể thức vòng bảng rồi loại trực tiếp mới chuyển từ vòng bảng sang knockout.");
        if (t.GroupCount is null || t.AdvancePerGroup is null)
            return Stop(a, "NO_GROUP_STAGE", "Giải chưa khai số bảng hoặc số đội đi tiếp mỗi bảng.");
        if (t.BracketLockedAt is null)
            return Stop(a, "NOT_LOCKED", "Nhánh đấu chưa khóa nên vòng bảng chưa bắt đầu.");

        var nodes = await _uow.Repository<BracketNode>().Query()
            .Where(n => n.TournamentId == t.TournamentId)
            .Select(n => new { n.BracketNodeId, n.StageNo, n.StageType, n.GroupLabel, n.RoundNo, n.PositionInRound, n.IsBye, n.SeedTeamId })
            .ToListAsync();
        var groupNodes = nodes.Where(n => n.StageType == "GROUP").ToList();
        if (groupNodes.Count == 0)
            return Stop(a, "NO_GROUP_STAGE", "Giải chưa có vòng bảng. Nhánh đấu hiện tại không có trận nào thuộc bảng đấu.");

        a.GroupStageNo = groupNodes.Max(n => n.StageNo);
        var groupOf = groupNodes.ToDictionary(n => n.BracketNodeId, n => n.GroupLabel ?? "?");
        var groupNodeIds = groupOf.Keys.ToList();
        var matches = await _uow.Repository<Match>().Query()
            .Where(m => groupNodeIds.Contains(m.BracketNodeId))
            .Select(m => new { m.BracketNodeId, m.TeamAid, m.TeamBid, m.ScoreA, m.ScoreB, m.WinnerTeamId, m.Status })
            .ToListAsync();

        var knockout = nodes.Where(n => n.StageType == "KNOCKOUT" && n.StageNo > a.GroupStageNo).ToList();
        var teamIds = matches.SelectMany(m => new[] { m.TeamAid, m.TeamBid }).Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();
        a.TeamNames = await _uow.Repository<Team>().Query()
            .Where(x => teamIds.Contains(x.TeamId))
            .ToDictionaryAsync(x => x.TeamId, x => x.Name);

        var advance = t.AdvancePerGroup.Value;
        var qualifiers = new List<IReadOnlyList<KnockoutDraw.Qualifier>>();
        var blocking = false;
        foreach (var group in matches.GroupBy(m => groupOf[m.BracketNodeId]).OrderBy(g => g.Key))
        {
            var decided = (decisions?.Decisions ?? new List<TieDecisionDto>())
                .Where(d => d.GroupLabel == group.Key)
                .Select(d => (IReadOnlyList<int>)d.TeamIds)
                .ToList();
            var rows = GroupStandingCalculator.Compute(
                group.Select(m => new GroupStandingCalculator.GroupMatch(m.TeamAid, m.TeamBid, m.ScoreA, m.ScoreB, m.WinnerTeamId, m.Status)),
                a.Order, decided, group.Key + "-");

            var table = new GroupTableDto { GroupLabel = group.Key };
            foreach (var r in rows)
                table.Rows.Add(new GroupRowDto
                {
                    Position = r.Position, TeamId = r.TeamId, TeamName = NameOf(a, r.TeamId),
                    Played = r.Played, Won = r.Won, Lost = r.Lost, GamesWon = r.GamesWon, GamesLost = r.GamesLost,
                    Diff = r.Diff, Points = r.Points, Qualified = r.Position <= advance && r.TieKey is null, TieKey = r.TieKey
                });
            foreach (var tie in rows.Where(r => r.TieKey is not null).GroupBy(r => r.TieKey!))
            {
                var first = tie.Min(r => r.Position);
                var last = first + tie.Count() - 1;
                var isBlocking = first <= advance;
                blocking |= isBlocking;
                table.Ties.Add(new GroupTieDto
                {
                    TieKey = tie.Key,
                    PositionsLabel = $"Hạng {first}–{last}",
                    Blocking = isBlocking,
                    TeamIds = tie.Select(r => r.TeamId).ToList(),
                    TeamNames = tie.Select(r => NameOf(a, r.TeamId)).ToList()
                });
            }
            a.Groups.Add(table);
            qualifiers.Add(rows.Where(r => r.Position <= advance).OrderBy(r => r.Position)
                .Select(r => new KnockoutDraw.Qualifier(r.TeamId, group.Key, r.Position)).ToList());
        }

        a.Total = matches.Count;
        a.Finished = matches.Count(m => GroupStandingCalculator.IsFinished(new GroupStandingCalculator.GroupMatch(m.TeamAid, m.TeamBid, m.ScoreA, m.ScoreB, m.WinnerTeamId, m.Status)));

        if (knockout.Count > 0)
        {
            a.State = "DONE";
            a.Message = "Đã tạo nhánh knockout cho giai đoạn " + knockout.Min(n => n.StageNo) + ".";
            a.Pairings = await ExistingPairingsAsync(a, knockout.Where(n => n.RoundNo == 1).Select(n => n.BracketNodeId).ToList(), qualifiers);
            return a;
        }
        if (a.Finished < a.Total)
            return Stop(a, "IN_PROGRESS", $"Vòng bảng còn {a.Total - a.Finished} trận chưa có kết quả.");
        if (blocking)
            return Stop(a, "NEEDS_DECISION", "Có đội bằng nhau sau mọi tiêu chí phân định ở vị trí ảnh hưởng tới suất đi tiếp. Ban tổ chức cần xếp thứ tự trước khi chuyển sang knockout.");
        if (qualifiers.Sum(q => q.Count) < 2)
            return Stop(a, "NO_KNOCKOUT", "Chỉ có một đội đi tiếp nên không cần vòng knockout.");

        a.State = "READY";
        a.Message = "Vòng bảng đã xong. Có thể tạo nhánh knockout.";
        a.Slots = KnockoutDraw.Build(qualifiers);
        a.Pairings = Enumerable.Range(0, a.Slots.Count / 2).Select(k =>
        {
            var x = a.Slots[2 * k];
            var y = a.Slots[2 * k + 1];
            var top = x ?? y!;
            var bottom = x is null ? null : y;
            return new KnockoutPairDto
            {
                PositionInRound = k + 1,
                TeamAName = NameOf(a, top.Team.TeamId), TeamALabel = top.Team.Label,
                TeamBName = bottom is null ? null : NameOf(a, bottom.Team.TeamId), TeamBLabel = bottom?.Team.Label,
                IsBye = bottom is null
            };
        }).ToList();
        return a;
    }

    private async Task<List<KnockoutPairDto>> ExistingPairingsAsync(Analysis a, List<int> roundOneNodeIds, List<IReadOnlyList<KnockoutDraw.Qualifier>> qualifiers)
    {
        var labels = qualifiers.SelectMany(q => q).GroupBy(q => q.TeamId).ToDictionary(g => g.Key, g => g.First().Label);
        var nodes = await _uow.Repository<BracketNode>().Query()
            .Where(n => roundOneNodeIds.Contains(n.BracketNodeId))
            .Select(n => new { n.PositionInRound, n.IsBye, n.SeedTeamId, Match = n.Matches.Select(m => new { m.TeamAid, m.TeamBid }).FirstOrDefault() })
            .ToListAsync();
        var ids = nodes.SelectMany(n => new[] { n.SeedTeamId, n.Match?.TeamAid, n.Match?.TeamBid }).Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();
        var names = await _uow.Repository<Team>().Query().Where(x => ids.Contains(x.TeamId)).ToDictionaryAsync(x => x.TeamId, x => x.Name);
        string N(int? id) => id.HasValue && names.TryGetValue(id.Value, out var n) ? n : "Chưa xác định";
        string L(int? id) => id.HasValue && labels.TryGetValue(id.Value, out var l) ? l : string.Empty;

        return nodes.OrderBy(n => n.PositionInRound).Select(n =>
        {
            var aId = n.IsBye ? n.SeedTeamId : n.Match?.TeamAid;
            var bId = n.IsBye ? null : n.Match?.TeamBid;
            return new KnockoutPairDto
            {
                PositionInRound = n.PositionInRound ?? 0,
                TeamAName = N(aId), TeamALabel = L(aId),
                TeamBName = n.IsBye ? null : N(bId), TeamBLabel = n.IsBye ? null : L(bId),
                IsBye = n.IsBye
            };
        }).ToList();
    }

    // Dựng toàn bộ cây rồi lưu một lần để không bao giờ còn nửa cây (lần chạy sau sẽ tưởng là đã tạo).
    private async Task<int> CreateKnockoutAsync(Analysis a)
    {
        var t = a.Tournament;
        var stageNo = a.GroupStageNo + 1;
        var now = DateTime.UtcNow;
        var size = a.Slots.Count;
        var totalRounds = (int)Math.Log2(size);

        BracketNode NewNode(int round, int position) => new()
        {
            TournamentId = t.TournamentId,
            StageNo = stageNo,
            StageType = "KNOCKOUT",
            RoundNo = round,
            PositionInRound = position,
            NodeType = "MATCH_SLOT",
            SeedingMethod = "BY_RANKING",
            Status = "LOCKED",
            GeneratedAt = now
        };
        Match NewMatch(BracketNode node, int? teamA, int? teamB, int round) => new()
        {
            TournamentId = t.TournamentId,
            BracketNode = node,
            TeamAid = teamA,
            TeamBid = teamB,
            BestOf = BestOfFor(t.BestOfPerRound, round, totalRounds),
            CheckInMode = "SELF",
            Status = "PENDING"
        };

        var level = new List<(BracketNode Node, int? Carried)>();
        for (var k = 0; k < size / 2; k++)
        {
            var x = a.Slots[2 * k];
            var y = a.Slots[2 * k + 1];
            var node = NewNode(1, k + 1);
            await _uow.Repository<BracketNode>().AddAsync(node);
            if (x is not null && y is not null)
            {
                await _uow.Repository<Match>().AddAsync(NewMatch(node, x.Team.TeamId, y.Team.TeamId, 1));
                level.Add((node, null));
            }
            else
            {
                // Bye: không tạo trận, đội được đưa thẳng vào trận vòng sau như BRK-01.
                var team = (x ?? y)!;
                node.IsBye = true;
                node.SeedTeamId = team.Team.TeamId;
                node.SeedNo = team.SeedNo;
                level.Add((node, team.Team.TeamId));
            }
        }

        for (var round = 2; round <= totalRounds; round++)
        {
            var next = new List<(BracketNode Node, int? Carried)>();
            for (var k = 0; k < level.Count / 2; k++)
            {
                var left = level[2 * k];
                var right = level[2 * k + 1];
                var parent = NewNode(round, k + 1);
                left.Node.ParentNode = parent;
                right.Node.ParentNode = parent;
                await _uow.Repository<BracketNode>().AddAsync(parent);
                await _uow.Repository<Match>().AddAsync(NewMatch(parent, left.Carried, right.Carried, round));
                next.Add((parent, null));
            }
            level = next;
        }

        await _uow.Repository<Notification>().AddAsync(new Notification
        {
            AccountId = t.OrganizerAccountId,
            NotificationType = "SYSTEM",
            Title = "Vòng bảng đã kết thúc",
            Body = $"Giải {t.Name} đã có nhánh knockout giai đoạn {stageNo} với {a.Slots.Count(s => s is not null)} đội đi tiếp. Hãy xếp lịch các trận.",
            LinkUrl = "/tournament/stage-transition.html?id=" + t.TournamentId,
            Channel = "IN_APP",
            IsRead = false,
            SentAt = now
        });

        await _uow.SaveChangesAsync();

        // UC-18 A1: tạo luôn trận tranh hạng Ba khi giải có (BRK-09). Lỗi ở đây không làm mất cây vừa tạo;
        // ThirdPlaceWorker sẽ tạo lại ở lần chạy sau.
        if (t.HasThirdPlaceMatch) await _thirdPlace.SyncAsync(t.TournamentId);
        return a.Slots.Count(s => s is not null);
    }

    // BestOfPerRound dạng "R1:1,R2:3,F:5": F là trận chung kết, Rn là vòng n của cây knockout. Mặc định BO1.
    public static int BestOfFor(string? config, int round, int totalRounds)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in (config ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var kv = part.Split(':', StringSplitOptions.TrimEntries);
            if (kv.Length == 2 && int.TryParse(kv[1], out var bo) && bo is 1 or 3 or 5 or 7) map[kv[0]] = bo;
        }
        if (round == totalRounds && map.TryGetValue("F", out var final)) return final;
        return map.TryGetValue("R" + round, out var value) ? value : 1;
    }

    // So với các nhóm hòa tính khi CHƯA áp quyết định: mỗi quyết định phải xếp đúng và đủ một nhóm hòa.
    private static string? InvalidDecision(Analysis undecided, AdvanceStageRequest? request)
    {
        foreach (var d in request?.Decisions ?? new List<TieDecisionDto>())
        {
            var group = undecided.Groups.FirstOrDefault(g => g.GroupLabel == d.GroupLabel);
            if (group is null) return $"Không có bảng {d.GroupLabel}.";
            var ids = d.TeamIds ?? new List<int>();
            var matches = ids.Count == ids.Distinct().Count()
                && group.Ties.Any(tie => tie.TeamIds.Count == ids.Count && tie.TeamIds.All(ids.Contains));
            if (!matches) return $"Thứ tự gửi lên không khớp nhóm đội hòa nào của bảng {d.GroupLabel}.";
        }
        return null;
    }

    private static Analysis Stop(Analysis a, string state, string message)
    {
        a.State = state;
        a.Message = message;
        return a;
    }

    private static string NameOf(Analysis a, int teamId) => a.TeamNames.TryGetValue(teamId, out var n) ? n : "Đội #" + teamId;

    private static StageTransitionBoardDto ToDto(Analysis a) => new()
    {
        TournamentId = a.Tournament.TournamentId,
        TournamentName = a.Tournament.Name,
        GroupCount = a.Tournament.GroupCount,
        AdvancePerGroup = a.Tournament.AdvancePerGroup,
        State = a.State,
        StateMessage = a.Message,
        FinishedMatches = a.Finished,
        TotalMatches = a.Total,
        TieBreakerOrder = a.Order,
        Groups = a.Groups,
        Pairings = a.Pairings,
        CanAdvance = a.State == "READY"
    };
}
