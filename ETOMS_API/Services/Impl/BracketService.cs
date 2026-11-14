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

        if (tournament.Format == "DOUBLE_ELIMINATION")
            return Result.Failure("Thể thức Loại kép (Double Elimination) chưa được hỗ trợ — vui lòng chọn thể thức khác.");

        var approved = await GetApprovedTeamsAsync(tournamentId);
        if (approved.Count < 2) return Result.Failure("Cần ít nhất 2 đội đã được duyệt để sinh nhánh đấu.");

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
            await _uow.SaveChangesAsync(); // cần NodeId để tạo Match

            if (!node.IsBye)
                await CreateMatchForNodeAsync(tournament, node, teamA, teamB);

            prevRound.Add(node);
        }

        int totalRounds = (int)Math.Log2(size);
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

                if (teamFromA.HasValue && teamFromB.HasValue)
                {
                    // Hai bye liên tiếp hiếm gặp: cả 2 đội đã biết trước, trận sẽ diễn ra bình thường.
                    parent.IsBye = false;
                    await _uow.SaveChangesAsync();
                    await CreateMatchForNodeAsync(tournament, parent, teamFromA, teamFromB);
                }
                else
                {
                    // Còn lại đều là trận thật (0 hoặc 1 bên đã biết trước), tạo Match với chỗ trống chờ advance.
                    parent.IsBye = false;
                    await _uow.SaveChangesAsync();
                    await CreateMatchForNodeAsync(tournament, parent, teamFromA, teamFromB);
                }

                thisRound.Add(parent);
            }
            prevRound = thisRound;
        }
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
                    await CreateMatchForNodeAsync(tournament, node, a, b);
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

    private async Task CreateMatchForNodeAsync(Tournament tournament, BracketNode node, int? teamA, int? teamB)
    {
        var match = new Match
        {
            TournamentId = tournament.TournamentId,
            BracketNodeId = node.BracketNodeId,
            TeamAid = teamA,
            TeamBid = teamB,
            BestOf = 1,
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
            .Select(t => new { t.TournamentId, t.Name, t.Format, t.Status, t.BracketLockedAt })
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
            ApprovedTeamCount = (await GetApprovedTeamsAsync(tournamentId)).Count
        };

        var nodes = await _uow.Repository<BracketNode>().Query()
            .Where(n => n.TournamentId == tournamentId)
            .Select(n => new
            {
                n.BracketNodeId,
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

        dto.Matches = nodes.Select(n => new BracketMatchDto
        {
            BracketNodeId = n.BracketNodeId,
            MatchId = n.Match?.MatchId,
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
