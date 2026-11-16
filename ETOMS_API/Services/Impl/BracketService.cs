using System.Data;
using ETOMS_API.Utils;
using ETOMS_API.DTOs.Bracket;
using ETOMS_API.Repositories;
using ETOMS_API.Repositories.Interface;
using ETOMS_API.Services.Interface;
using ETOMS_API.Models;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Services.Impl;

// Module BRK: BRK-01 (sinh nhánh + byes), BRK-02 (xếp hạt giống), BRK-03 (khóa nhánh),
// BRK-04 (Single Elimination), BRK-06 (Round Robin).
//
// 1 BracketNode = 1 trận. Node bye (số đội lẻ) có IsBye=true + SeedTeamId, không tạo Match.
public class BracketService : IBracketService
{
    private readonly IUnitOfWork _uow;

    public BracketService(IUnitOfWork uow) => _uow = uow;

    public async Task<IReadOnlyList<SeedingTeamDto>> GetApprovedTeamsAsync(int tournamentId)
    {
        var teams = await _uow.Repository<Registration>().Query()
            .Where(r => r.TournamentId == tournamentId && r.Status == "APPROVED")
            .OrderBy(r => r.ReviewedAt)
            .Select(r => new { r.Team.TeamId, r.Team.Name, r.Team.Tag })
            .ToListAsync();

        var list = new List<SeedingTeamDto>();
        for (int i = 0; i < teams.Count; i++)
            list.Add(new SeedingTeamDto { TeamId = teams[i].TeamId, Name = teams[i].Name, Tag = teams[i].Tag, SeedRank = i + 1 });
        return list;
    }

    public async Task<bool> IsOrganizerAsync(int tournamentId, int accountId)
        => await _uow.Repository<Tournament>().AnyAsync(t => t.TournamentId == tournamentId && t.OrganizerAccountId == accountId);

    // ===== BRK-01 + BRK-02 + BRK-04 + BRK-06 =====
    public async Task<Result> GenerateBracketAsync(int tournamentId, GenerateBracketRequest request, int organizerAccountId)
    {
        var tournament = await _uow.Repository<Tournament>().GetByIdAsync(tournamentId);
        if (tournament is null) return Result.Failure("Không tìm thấy giải đấu.");
        if (tournament.OrganizerAccountId != organizerAccountId) return Result.Failure("Chỉ ban tổ chức của giải được sinh nhánh đấu.");
        if (tournament.BracketLockedAt != null) return Result.Failure("Nhánh đấu đã khóa, không thể sinh lại.");
        if (tournament.Status is "COMPLETED" or "CANCELLED") return Result.Failure("Giải đã kết thúc hoặc đã hủy.");
        // UC-20: Ban tổ chức đóng đăng ký rồi mới sinh nhánh — còn nhận đăng ký thì danh sách đội chưa chốt.
        if (DateTime.UtcNow < tournament.RegCloseAt)
            return Result.Failure($"Giải còn nhận đăng ký đến {tournament.RegCloseAt.AddHours(7):HH:mm dd/MM/yyyy}. Chỉ sinh nhánh sau khi đóng đăng ký.");

        var approved = await GetApprovedTeamsAsync(tournamentId);
        if (approved.Count < 2) return Result.Failure("Cần ít nhất 2 đội đã được duyệt để sinh nhánh đấu.");
        // Loại kép 2 đội chỉ là đánh lại đúng một cặp ở nhánh thua — cần ít nhất 3 đội mới có nhánh thua thật.
        if (DoubleElimination.IsDoubleElimination(tournament.Format) && approved.Count < DoubleElimination.MinTeams)
            return Result.Failure($"Thể thức Loại kép cần ít nhất {DoubleElimination.MinTeams} đội đã được duyệt.");

        // Xác định thứ tự hạt giống theo phương thức đã chọn.
        List<int> seedOrder;
        int? randomSeed = null;
        switch (request.SeedingMethod)
        {
            case "RANDOM_DRAW":
                randomSeed = Environment.TickCount;
                var rng = new Random(randomSeed.Value);
                seedOrder = approved.Select(t => t.TeamId).OrderBy(_ => rng.Next()).ToList();
                break;
            case "MANUAL":
                var approvedIds = approved.Select(t => t.TeamId).ToHashSet();
                if (request.ManualOrder is null || request.ManualOrder.Count != approved.Count || !request.ManualOrder.All(approvedIds.Contains))
                    return Result.Failure("Thứ tự thủ công không hợp lệ — phải đủ và đúng danh sách đội đã duyệt.");
                seedOrder = request.ManualOrder;
                break;
            default: // BY_RANKING — theo thứ tự duyệt đơn
                seedOrder = approved.Select(t => t.TeamId).ToList();
                break;
        }

        // Idempotent: xóa cây cũ trước khi dựng lại — phải xóa các bảng phụ thuộc Match trước
        // (MatchReport, Dispute, Livestream, MatchChatMessage, MatchGame, MatchRequest, Penalty).
        // Bọc trong 1 transaction: lỗi giữa chừng (vd sinh cây mới thất bại) phải rollback lại
        // cây CŨ, không để lại trạng thái dở dang (đã xóa cũ nhưng chưa có cây mới).
        await using var transaction = await _uow.BeginTransactionAsync(IsolationLevel.ReadCommitted);

        var oldMatches = await _uow.Repository<Match>().Query().Where(m => m.TournamentId == tournamentId).ToListAsync();
        var oldMatchIds = oldMatches.Select(m => m.MatchId).ToList();
        if (oldMatchIds.Count > 0)
        {
            foreach (var r in await _uow.Repository<MatchReport>().Query().Where(r => oldMatchIds.Contains(r.MatchId)).ToListAsync())
                _uow.Repository<MatchReport>().Remove(r);
            foreach (var d in await _uow.Repository<Dispute>().Query().Where(d => oldMatchIds.Contains(d.MatchId)).ToListAsync())
                _uow.Repository<Dispute>().Remove(d);
            foreach (var l in await _uow.Repository<Livestream>().Query().Where(l => l.MatchId.HasValue && oldMatchIds.Contains(l.MatchId.Value)).ToListAsync())
                _uow.Repository<Livestream>().Remove(l);
            foreach (var c in await _uow.Repository<MatchChatMessage>().Query().Where(c => oldMatchIds.Contains(c.MatchId)).ToListAsync())
                _uow.Repository<MatchChatMessage>().Remove(c);
            foreach (var g in await _uow.Repository<MatchGame>().Query().Where(g => oldMatchIds.Contains(g.MatchId)).ToListAsync())
                _uow.Repository<MatchGame>().Remove(g);
            foreach (var req in await _uow.Repository<MatchRequest>().Query().Where(req => oldMatchIds.Contains(req.MatchId)).ToListAsync())
                _uow.Repository<MatchRequest>().Remove(req);
            foreach (var p in await _uow.Repository<Penalty>().Query().Where(p => p.MatchId.HasValue && oldMatchIds.Contains(p.MatchId.Value)).ToListAsync())
                _uow.Repository<Penalty>().Remove(p);
            await _uow.SaveChangesAsync();
        }
        foreach (var m in oldMatches) _uow.Repository<Match>().Remove(m);
        var oldNodes = await _uow.Repository<BracketNode>().Query().Where(n => n.TournamentId == tournamentId).ToListAsync();
        foreach (var n in oldNodes) _uow.Repository<BracketNode>().Remove(n);
        await _uow.SaveChangesAsync();

        // HYBRID dùng chung bộ sinh vòng bảng với ROUND_ROBIN (đã chia theo Tournament.GroupCount) —
        // vòng knockout sau vòng bảng do StageTransitionService (BRK-08) tạo riêng khi vòng bảng xong.
        if (tournament.Format is "ROUND_ROBIN" or "HYBRID")
            await GenerateRoundRobinAsync(tournament, seedOrder, request.SeedingMethod, randomSeed);
        else if (DoubleElimination.IsDoubleElimination(tournament.Format))
            await GenerateDoubleEliminationAsync(tournament, seedOrder, request.SeedingMethod, randomSeed);
        else
            await GenerateSingleEliminationAsync(tournament, seedOrder, request.SeedingMethod, randomSeed);

        await transaction.CommitAsync();
        return Result.Success("Đã sinh nhánh đấu thành công.");
    }

    // ===== BRK-04: thuật toán Single Elimination (có byes) =====
    private async Task GenerateSingleEliminationAsync(Tournament tournament, List<int> seedOrder, string seedingMethod, int? randomSeed)
    {
        int n = seedOrder.Count;
        int size = NextPowerOfTwo(n);
        var positions = StandardSeedOrder(size); // positions[i] = seed-rank (1-based) đứng ở vị trí i (0-based)

        int?[] slots = new int?[size];
        for (int i = 0; i < size; i++)
        {
            int seedRank = positions[i];
            slots[i] = seedRank <= n ? seedOrder[seedRank - 1] : (int?)null;
        }

        int totalRounds = (int)Math.Log2(size);

        var prevRound = new List<BracketNode>();
        for (int k = 0; k < size / 2; k++)
        {
            int? teamA = slots[2 * k];
            int? teamB = slots[2 * k + 1];

            var node = new BracketNode
            {
                TournamentId = tournament.TournamentId,
                StageNo = 1,
                StageType = "KNOCKOUT",
                RoundNo = 1,
                PositionInRound = k + 1,
                NodeType = "MATCH_SLOT",
                SeedingMethod = seedingMethod,
                SeedingRandomSeed = randomSeed,
                Status = "DRAFT"
            };

            if (teamA.HasValue && teamB.HasValue)
            {
                node.IsBye = false;
            }
            else if (teamA.HasValue || teamB.HasValue)
            {
                node.IsBye = true;
                node.SeedTeamId = teamA ?? teamB;
            }
            await _uow.Repository<BracketNode>().AddAsync(node);
            await _uow.SaveChangesAsync();

            if (!node.IsBye)
                await CreateMatchForNodeAsync(tournament, node, teamA, teamB, 1, totalRounds);

            prevRound.Add(node);
        }

        for (int r = 2; r <= totalRounds; r++)
        {
            var thisRound = new List<BracketNode>();
            for (int k = 0; k < prevRound.Count / 2; k++)
            {
                var childA = prevRound[2 * k];
                var childB = prevRound[2 * k + 1];

                var parent = new BracketNode
                {
                    TournamentId = tournament.TournamentId,
                    StageNo = 1,
                    StageType = "KNOCKOUT",
                    RoundNo = r,
                    PositionInRound = k + 1,
                    NodeType = "MATCH_SLOT",
                    SeedingMethod = seedingMethod,
                    SeedingRandomSeed = randomSeed,
                    Status = "DRAFT"
                };
                await _uow.Repository<BracketNode>().AddAsync(parent);
                await _uow.SaveChangesAsync();

                childA.ParentNodeId = parent.BracketNodeId;
                childB.ParentNodeId = parent.BracketNodeId;
                _uow.Repository<BracketNode>().Update(childA);
                _uow.Repository<BracketNode>().Update(childB);

                int? teamFromA = childA.IsBye ? childA.SeedTeamId : null;
                int? teamFromB = childB.IsBye ? childB.SeedTeamId : null;

                parent.IsBye = false;
                await _uow.SaveChangesAsync();
                await CreateMatchForNodeAsync(tournament, parent, teamFromA, teamFromB, r, totalRounds);

                thisRound.Add(parent);
            }
            prevRound = thisRound;
        }
    }

    // ===== BRK-05: Loại kép (Double Elimination) — xem DoubleElimination.cs =====
    // Nhánh thắng xếp hạt giống như loại trực tiếp (có miễn đấu vòng 1); nhánh thua và chung kết tổng sinh sẵn,
    // trận để trống đội và được điền khi có kết quả (ResultService đẩy đội thắng + đội thua).
    private async Task GenerateDoubleEliminationAsync(Tournament tournament, List<int> seedOrder, string seedingMethod, int? randomSeed)
    {
        int n = seedOrder.Count;
        int size = NextPowerOfTwo(n);
        int k = (int)Math.Log2(size);
        var positions = StandardSeedOrder(size);
        int?[] slots = new int?[size];
        for (int i = 0; i < size; i++) slots[i] = positions[i] <= n ? seedOrder[positions[i] - 1] : null;

        BracketNode NewNode(int stage, int round, int pos) => new()
        {
            TournamentId = tournament.TournamentId,
            StageNo = stage,
            StageType = "KNOCKOUT",
            RoundNo = round,
            PositionInRound = pos,
            NodeType = "MATCH_SLOT",
            SeedingMethod = seedingMethod,
            SeedingRandomSeed = randomSeed,
            Status = "DRAFT",
            IsBye = false
        };

        var nodes = new List<BracketNode>();
        var byeR1 = new bool[size / 2];
        for (int r = 1; r <= k; r++)
            for (int p = 1; p <= size >> r; p++)
            {
                var node = NewNode(DoubleElimination.WinnersStage, r, p);
                if (r == 1)
                {
                    int? a = slots[2 * p - 2], b = slots[2 * p - 1];
                    if (a is null || b is null) { node.IsBye = true; node.SeedTeamId = a ?? b; byeR1[p - 1] = true; }
                }
                nodes.Add(node);
            }
        for (int r = 1; r <= 2 * (k - 1); r++)
            for (int p = 1; p <= DoubleElimination.LosersRoundCount(size, r); p++)
                if (DoubleElimination.LosersNodeExists(r, p, byeR1))
                    nodes.Add(NewNode(DoubleElimination.LosersStage, r, p));
        nodes.Add(NewNode(DoubleElimination.GrandFinalStage, 1, 1));

        foreach (var node in nodes) await _uow.Repository<BracketNode>().AddAsync(node);
        await _uow.SaveChangesAsync();

        // Nối đường đi của đội thắng; đội miễn vòng 1 vào sẵn ô của trận vòng 2.
        var layout = new DoubleElimination.Layout(nodes);
        var prefill = new Dictionary<int, (int? A, int? B)>();
        foreach (var node in nodes)
        {
            var to = layout.WinnerTo(node);
            node.ParentNodeId = to?.Node.BracketNodeId;
            _uow.Repository<BracketNode>().Update(node);
            if (to is not null && node.IsBye && node.SeedTeamId is int seeded)
            {
                prefill.TryGetValue(to.Node.BracketNodeId, out var cur);
                prefill[to.Node.BracketNodeId] = to.SideA ? (seeded, cur.B) : (cur.A, seeded);
            }
        }
        await _uow.SaveChangesAsync();

        foreach (var node in nodes.Where(x => !x.IsBye))
        {
            int? a = null, b = null;
            if (node.StageNo == DoubleElimination.WinnersStage && node.RoundNo == 1)
            {
                a = slots[2 * node.PositionInRound!.Value - 2];
                b = slots[2 * node.PositionInRound!.Value - 1];
            }
            else if (prefill.TryGetValue(node.BracketNodeId, out var pre)) (a, b) = pre;
            // Lưu một lần cho cả nhánh (DB chung ở xa, mỗi lần lưu là một lượt mạng).
            await _uow.Repository<Match>().AddAsync(new Match
            {
                TournamentId = tournament.TournamentId,
                BracketNodeId = node.BracketNodeId,
                TeamAid = a,
                TeamBid = b,
                BestOf = StageTransitionService.BestOfFor(tournament.BestOfPerRound, layout.BestOfRound(node), k),
                CheckInMode = "SELF",
                Status = "PENDING"
            });
        }
        await _uow.SaveChangesAsync();
    }

    // ===== BRK-06: thuật toán vòng tròn Round Robin =====
    private async Task GenerateRoundRobinAsync(Tournament tournament, List<int> seedOrder, string seedingMethod, int? randomSeed)
    {
        int groupCount = Math.Max(1, tournament.GroupCount ?? 1);
        var groups = new List<List<int>>();
        for (int g = 0; g < groupCount; g++) groups.Add(new List<int>());
        // Chia đều theo kiểu rắn bò (snake) để các bảng cân sức theo hạt giống.
        for (int i = 0; i < seedOrder.Count; i++)
        {
            int cycle = i / groupCount;
            int g = cycle % 2 == 0 ? i % groupCount : groupCount - 1 - (i % groupCount);
            groups[g].Add(seedOrder[i]);
        }

        for (int g = 0; g < groupCount; g++)
        {
            var label = ((char)('A' + g)).ToString();
            await GenerateRoundRobinScheduleAsync(tournament, groups[g], label, seedingMethod, randomSeed);
        }
    }

    private async Task GenerateRoundRobinScheduleAsync(Tournament tournament, List<int> teamIds, string groupLabel, string seedingMethod, int? randomSeed)
    {
        var teams = teamIds.Select(t => (int?)t).ToList();
        if (teams.Count % 2 == 1) teams.Add(null); // đội lẻ -> thêm "nghỉ" ảo
        int n = teams.Count;
        if (n < 2) return;

        int rounds = n - 1;
        int posCounter = 1;
        for (int r = 0; r < rounds; r++)
        {
            for (int i = 0; i < n / 2; i++)
            {
                var a = teams[i];
                var b = teams[n - 1 - i];
                if (a.HasValue && b.HasValue)
                {
                    var node = new BracketNode
                    {
                        TournamentId = tournament.TournamentId,
                        StageNo = 1,
                        StageType = "GROUP",
                        GroupLabel = groupLabel,
                        RoundNo = r + 1,
                        PositionInRound = posCounter++,
                        NodeType = "MATCH_SLOT",
                        IsBye = false,
                        SeedingMethod = seedingMethod,
                        SeedingRandomSeed = randomSeed,
                        Status = "DRAFT"
                    };
                    await _uow.Repository<BracketNode>().AddAsync(node);
                    await _uow.SaveChangesAsync();
                    await CreateMatchForNodeAsync(tournament, node, a, b, 1, 1);
                }
            }
            // Xoay vòng: giữ phần tử đầu cố định, các phần tử còn lại xoay 1 bước.
            var fixedTeam = teams[0];
            var rest = teams.Skip(1).ToList();
            rest.Insert(0, rest[^1]);
            rest.RemoveAt(rest.Count - 1);
            teams = new List<int?> { fixedTeam };
            teams.AddRange(rest);
        }
    }

    private async Task CreateMatchForNodeAsync(Tournament tournament, BracketNode node, int? teamA, int? teamB, int round, int totalRounds)
    {
        var match = new Match
        {
            TournamentId = tournament.TournamentId,
            BracketNodeId = node.BracketNodeId,
            TeamAid = teamA,
            TeamBid = teamB,
            BestOf = StageTransitionService.BestOfFor(tournament.BestOfPerRound, round, totalRounds),
            CheckInMode = "SELF",
            Status = "PENDING"
        };
        await _uow.Repository<Match>().AddAsync(match);
        await _uow.SaveChangesAsync();
    }

    private static int NextPowerOfTwo(int n)
    {
        int p = 1;
        while (p < n) p *= 2;
        return p;
    }

    // Thứ tự xếp vị trí chuẩn (standard tournament seeding) — seed mạnh gặp byes/seed yếu trước.
    private static List<int> StandardSeedOrder(int size)
    {
        if (size == 1) return new List<int> { 1 };
        var prev = StandardSeedOrder(size / 2);
        var result = new List<int>();
        foreach (var p in prev)
        {
            result.Add(p);
            result.Add(size + 1 - p);
        }
        return result;
    }

    public async Task<BracketTreeDto?> GetBracketAsync(int tournamentId)
    {
        var tournament = await _uow.Repository<Tournament>().Query()
            .Where(t => t.TournamentId == tournamentId)
            .Select(t => new { t.TournamentId, t.Name, t.Format, t.Status, t.BracketLockedAt, t.RegCloseAt })
            .FirstOrDefaultAsync();
        if (tournament is null) return null;

        var dto = new BracketTreeDto
        {
            TournamentId = tournament.TournamentId,
            TournamentName = tournament.Name,
            Format = tournament.Format,
            TournamentStatus = tournament.Status,
            IsLocked = tournament.BracketLockedAt != null,
            LockedAt = tournament.BracketLockedAt,
            ApprovedTeamCount = (await GetApprovedTeamsAsync(tournamentId)).Count,
            RegCloseAt = tournament.RegCloseAt,
            RegistrationStillOpen = DateTime.UtcNow < tournament.RegCloseAt
        };

        var nodes = await _uow.Repository<BracketNode>().Query()
            .Where(n => n.TournamentId == tournamentId)
            .Select(n => new
            {
                n.BracketNodeId,
                n.StageNo,
                n.ParentNodeId,
                n.StageType,
                n.GroupLabel,
                n.RoundNo,
                n.PositionInRound,
                n.IsBye,
                n.SeedTeamId,
                SeedTeamName = n.SeedTeam != null ? n.SeedTeam.Name : null,
                Match = n.Matches.Select(m => new
                {
                    m.MatchId,
                    m.TeamAid,
                    TeamAName = m.TeamA != null ? m.TeamA.Name : null,
                    m.TeamBid,
                    TeamBName = m.TeamB != null ? m.TeamB.Name : null,
                    m.WinnerTeamId,
                    WinnerTeamName = m.WinnerTeam != null ? m.WinnerTeam.Name : null
                }).FirstOrDefault()
            })
            .ToListAsync();

        // Loại kép: nhãn theo nhánh (Nhánh thắng · Bán kết, Nhánh thua · Vòng 2, Chung kết tổng).
        var de = DoubleElimination.IsDoubleElimination(tournament.Format)
            ? new DoubleElimination.Layout(nodes.Select(n => new BracketNode
            {
                BracketNodeId = n.BracketNodeId, StageNo = n.StageNo, StageType = n.StageType, RoundNo = n.RoundNo,
                PositionInRound = n.PositionInRound, ParentNodeId = n.ParentNodeId
            }))
            : null;

        dto.Matches = nodes.Select(n => new BracketMatchDto
        {
            BracketNodeId = n.BracketNodeId,
            MatchId = n.Match?.MatchId,
            StageNo = n.StageNo,
            BracketLabel = de?.Nodes.FirstOrDefault(x => x.BracketNodeId == n.BracketNodeId) is { } dn ? de.MatchLabel(dn) : null,
            StageType = n.StageType,
            GroupLabel = n.GroupLabel,
            RoundNo = n.RoundNo ?? 0,
            PositionInRound = n.PositionInRound ?? 0,
            IsBye = n.IsBye,
            TeamAId = n.IsBye ? n.SeedTeamId : n.Match?.TeamAid,
            TeamAName = n.IsBye ? n.SeedTeamName : n.Match?.TeamAName,
            TeamBId = n.Match?.TeamBid,
            TeamBName = n.Match?.TeamBName,
            WinnerTeamId = n.IsBye ? n.SeedTeamId : n.Match?.WinnerTeamId,
            WinnerTeamName = n.IsBye ? n.SeedTeamName : n.Match?.WinnerTeamName
        }).ToList();

        return dto;
    }

    // ===== BRK-03: Khóa nhánh đấu =====
    public async Task<Result> LockBracketAsync(int tournamentId, int organizerAccountId)
    {
        var tournament = await _uow.Repository<Tournament>().GetByIdAsync(tournamentId);
        if (tournament is null) return Result.Failure("Không tìm thấy giải đấu.");
        if (tournament.OrganizerAccountId != organizerAccountId) return Result.Failure("Chỉ ban tổ chức của giải được khóa nhánh đấu.");
        if (tournament.BracketLockedAt != null) return Result.Failure("Nhánh đấu đã được khóa trước đó.");

        var hasBracket = await _uow.Repository<BracketNode>().AnyAsync(n => n.TournamentId == tournamentId);
        if (!hasBracket) return Result.Failure("Chưa sinh nhánh đấu, không thể khóa.");

        tournament.BracketLockedAt = DateTime.UtcNow;
        tournament.BracketLockedByAccountId = organizerAccountId;
        _uow.Repository<Tournament>().Update(tournament);

        var nodes = await _uow.Repository<BracketNode>().Query().Where(n => n.TournamentId == tournamentId).ToListAsync();
        foreach (var n in nodes) { n.Status = "LOCKED"; _uow.Repository<BracketNode>().Update(n); }

        await _uow.SaveChangesAsync();
        return Result.Success("Đã khóa nhánh đấu. Roster các đội và cổng đăng ký đã đóng.");
    }

    // Trận "chưa đấu": chưa có người thắng, chưa có tỉ số, còn ở trạng thái chờ.
    private static bool NotPlayed(Match m) =>
        m.WinnerTeamId == null && m.ScoreA == null && m.ScoreB == null && (m.Status == "PENDING" || m.Status == "READY");

    public async Task<Result> UnlockBracketAsync(int tournamentId, int organizerAccountId)
    {
        var tournament = await _uow.Repository<Tournament>().GetByIdAsync(tournamentId);
        if (tournament is null) return Result.Failure("Không tìm thấy giải đấu.");
        if (tournament.OrganizerAccountId != organizerAccountId) return Result.Failure("Chỉ ban tổ chức của giải được mở khóa nhánh đấu.");
        if (tournament.BracketLockedAt == null) return Result.Failure("Nhánh đấu chưa bị khóa.");
        if (tournament.Status is "COMPLETED" or "CANCELLED") return Result.Failure("Giải đã kết thúc hoặc đã hủy, không thể mở khóa nhánh.");

        var matches = await _uow.Repository<Match>().Query().Where(m => m.TournamentId == tournamentId).ToListAsync();
        if (matches.Any(m => m.Status != "CANCELLED" && !NotPlayed(m)))
            return Result.Failure("Đã có trận bắt đầu hoặc có kết quả, không thể mở khóa nhánh đấu.");

        tournament.BracketLockedAt = null;
        tournament.BracketLockedByAccountId = null;
        _uow.Repository<Tournament>().Update(tournament);

        var nodes = await _uow.Repository<BracketNode>().Query().Where(n => n.TournamentId == tournamentId).ToListAsync();
        foreach (var n in nodes) { n.Status = "DRAFT"; _uow.Repository<BracketNode>().Update(n); }

        await _uow.SaveChangesAsync();
        return Result.Success("Đã mở khóa nhánh đấu — có thể đổi chỗ đội hoặc sinh lại nhánh.");
    }

    // Đổi chỗ 2 đội ở MỌI vị trí của họ trong nhánh (ô hạt giống, trận vòng 1, trận vòng bảng, ô được
    // miễn vòng đi tiếp) — nên dùng chung cho cả cây knockout lẫn vòng bảng.
    public async Task<Result> SwapTeamsAsync(int tournamentId, int teamAId, int teamBId, int organizerAccountId)
    {
        var tournament = await _uow.Repository<Tournament>().GetByIdAsync(tournamentId);
        if (tournament is null) return Result.Failure("Không tìm thấy giải đấu.");
        if (tournament.OrganizerAccountId != organizerAccountId) return Result.Failure("Chỉ ban tổ chức của giải được sửa nhánh đấu.");
        if (tournament.BracketLockedAt != null) return Result.Failure("Nhánh đấu đã khóa — hãy mở khóa trước khi đổi chỗ đội.");
        if (teamAId == teamBId) return Result.Failure("Hãy kéo sang một đội khác.");

        var nodes = await _uow.Repository<BracketNode>().Query()
            .Where(n => n.TournamentId == tournamentId && (n.SeedTeamId == teamAId || n.SeedTeamId == teamBId)).ToListAsync();
        var matches = await _uow.Repository<Match>().Query()
            .Where(m => m.TournamentId == tournamentId
                && (m.TeamAid == teamAId || m.TeamBid == teamAId || m.TeamAid == teamBId || m.TeamBid == teamBId))
            .ToListAsync();

        bool InBracket(int id) => nodes.Any(n => n.SeedTeamId == id) || matches.Any(m => m.TeamAid == id || m.TeamBid == id);
        if (!InBracket(teamAId) || !InBracket(teamBId)) return Result.Failure("Đội không nằm trong nhánh đấu của giải này.");
        if (matches.Any(m => m.Status != "CANCELLED" && !NotPlayed(m)))
            return Result.Failure("Một trong hai đội đã thi đấu hoặc có kết quả, không thể đổi chỗ.");

        int Swap(int? id) => id == teamAId ? teamBId : teamAId;
        foreach (var n in nodes) { n.SeedTeamId = Swap(n.SeedTeamId); _uow.Repository<BracketNode>().Update(n); }
        foreach (var m in matches)
        {
            if (m.TeamAid == teamAId || m.TeamAid == teamBId) m.TeamAid = Swap(m.TeamAid);
            if (m.TeamBid == teamAId || m.TeamBid == teamBId) m.TeamBid = Swap(m.TeamBid);
            _uow.Repository<Match>().Update(m);
        }

        await _uow.SaveChangesAsync();
        return Result.Success("Đã đổi chỗ 2 đội trong nhánh đấu.");
    }

    // Cầu nối tạm PickWinner đã bỏ — kết quả giờ vào qua IResultService (RES-01/02/07), đúng LI-01.
    // AdvanceWinner sau khi có kết quả nằm trong ResultService.

    // BRK-07: chưa cấu hình thì trả về thứ tự mặc định (IsDefault = true), không tự lưu vào DB.
    public async Task<TieBreakerConfigDto?> GetTieBreakerConfigAsync(int tournamentId)
    {
        var tournament = await _uow.Repository<Tournament>().GetByIdAsync(tournamentId);
        if (tournament is null) return null;

        var isDefault = string.IsNullOrWhiteSpace(tournament.TieBreakerOrder);
        var order = isDefault
            ? TieBreakerCriteria.DefaultOrder.ToList()
            : tournament.TieBreakerOrder!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        return new TieBreakerConfigDto { TournamentId = tournamentId, IsDefault = isDefault, Order = order };
    }

    public async Task<Result> SetTieBreakerConfigAsync(int tournamentId, List<string> order, int organizerAccountId)
    {
        var tournament = await _uow.Repository<Tournament>().GetByIdAsync(tournamentId);
        if (tournament is null) return Result.Failure("Không tìm thấy giải đấu.");
        if (tournament.OrganizerAccountId != organizerAccountId)
            return Result.Failure("Chỉ ban tổ chức của giải được cấu hình tiêu chí phân định.");

        var distinctValid = order.Distinct().ToList();
        if (distinctValid.Count != TieBreakerCriteria.DefaultOrder.Count
            || !TieBreakerCriteria.DefaultOrder.All(distinctValid.Contains))
            return Result.FieldFailure(nameof(order), "Phải chọn đủ 4 tiêu chí, mỗi tiêu chí đúng một lần.");

        tournament.TieBreakerOrder = string.Join(',', order);
        _uow.Repository<Tournament>().Update(tournament);
        await _uow.SaveChangesAsync();
        return Result.Success("Đã lưu thứ tự ưu tiên tiêu chí phân định.");
    }
}
