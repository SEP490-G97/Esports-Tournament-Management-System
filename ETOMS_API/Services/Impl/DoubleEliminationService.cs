using ETOMS_API.Models;
using ETOMS_API.Repositories;
using ETOMS_API.Services;
using ETOMS_API.Services.Interface;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Services.Impl;

public class DoubleEliminationService : IDoubleEliminationService
{
    public const string LabelUpper = "UB";
    public const string LabelLower = "LB";
    public const string LabelGrand = "GF";

    private static readonly int[] AllowedBestOf = { 1, 3, 5, 7 };

    private readonly EtomsDbContext _db;

    public DoubleEliminationService(EtomsDbContext db) => _db = db;

    private static string Label(BracketSide s) => s switch
    {
        BracketSide.Upper => LabelUpper,
        BracketSide.Lower => LabelLower,
        _ => LabelGrand
    };

    private static bool Editable(string? status) => status == "PENDING" || status == "READY";

    // ------------------------------------------------------------------ sinh nhánh

    public async Task<BracketGenerateResult> GenerateAsync(int tournamentId, IReadOnlyList<int> seededTeamIds,
        DoubleEliminationOptions? options = null, CancellationToken ct = default)
    {
        options ??= new DoubleEliminationOptions();
        int bestOfGf = options.BestOfGrandFinal == 0 ? options.BestOf : options.BestOfGrandFinal;
        if (!AllowedBestOf.Contains(options.BestOf) || !AllowedBestOf.Contains(bestOfGf))
            return new(BracketError.InvalidBestOf);

        var t = await _db.Set<Tournament>().FirstOrDefaultAsync(x => x.TournamentId == tournamentId, ct);
        if (t == null) return new(BracketError.TournamentNotFound);
        if (t.Format != "DOUBLE_ELIMINATION") return new(BracketError.WrongFormat);
        if (t.Status == "COMPLETED" || t.Status == "CANCELLED") return new(BracketError.TournamentClosed);
        if (t.BracketLockedAt != null) return new(BracketError.BracketLocked);

        var teams = seededTeamIds ?? Array.Empty<int>();
        if (teams.Count < DoubleEliminationPlanner.MinTeams || teams.Count > DoubleEliminationPlanner.MaxTeams
            || teams.Count > t.MaxTeams)
            return new(BracketError.TeamCountInvalid);
        if (teams.Distinct().Count() != teams.Count) return new(BracketError.DuplicateTeams);

        var found = await _db.Set<Team>().Where(x => teams.Contains(x.TeamId)).Select(x => x.TeamId).ToListAsync(ct);
        if (found.Count != teams.Count) return new(BracketError.TeamNotFound);

        var oldMatches = await _db.Set<Match>().Where(m => m.TournamentId == tournamentId).ToListAsync(ct);
        if (oldMatches.Any(m => !Editable(m.Status))) return new(BracketError.MatchesStarted);

        var plan = DoubleEliminationPlanner.Build(teams.Count, options.GrandFinalReset);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        // Dọn nhánh cũ: trận trước, rồi gỡ khóa cha-con giữa các nút, rồi xóa nút.
        var oldNodes = await _db.Set<BracketNode>().Where(n => n.TournamentId == tournamentId).ToListAsync(ct);
        if (oldMatches.Count > 0) { _db.Set<Match>().RemoveRange(oldMatches); await _db.SaveChangesAsync(ct); }
        if (oldNodes.Count > 0)
        {
            foreach (var n in oldNodes) n.ParentNodeId = null;
            await _db.SaveChangesAsync(ct);
            _db.Set<BracketNode>().RemoveRange(oldNodes);
            await _db.SaveChangesAsync(ct);
        }
        _db.ChangeTracker.Clear();

        var now = DateTime.UtcNow;
        var nodes = new Dictionary<int, BracketNode>();
        foreach (var pm in plan.Matches)
        {
            var node = new BracketNode
            {
                TournamentId = tournamentId,
                StageNo = 1,
                StageType = "KNOCKOUT",
                GroupLabel = Label(pm.Side),
                RoundNo = pm.Round,
                PositionInRound = pm.Position,
                NodeType = "MATCH_SLOT",
                IsBye = false,
                SeedingMethod = "MANUAL",
                Status = "DRAFT",
                GeneratedAt = now
            };
            nodes[pm.No] = node;
            _db.Set<BracketNode>().Add(node);
        }
        await _db.SaveChangesAsync(ct);

        // ParentNodeId = nút kế tiếp của đội thắng; GF1 → GF2.
        foreach (var pm in plan.Matches)
        {
            int? parent = pm.WinnerTo?.MatchNo;
            if (pm.No == plan.GrandFinalNo && plan.ResetNo != null) parent = plan.ResetNo;
            if (parent != null) nodes[pm.No].ParentNodeId = nodes[parent.Value].BracketNodeId;
        }
        await _db.SaveChangesAsync(ct);

        foreach (var pm in plan.Matches)
        {
            int? a = pm.IsReset || pm.A.Kind != SourceKind.Seed ? null : teams[pm.A.Ref - 1];
            int? b = pm.IsReset || pm.B.Kind != SourceKind.Seed ? null : teams[pm.B.Ref - 1];
            _db.Set<Match>().Add(new Match
            {
                TournamentId = tournamentId,
                BracketNodeId = nodes[pm.No].BracketNodeId,
                TeamAid = a,
                TeamBid = b,
                BestOf = pm.Side == BracketSide.Grand ? bestOfGf : options.BestOf,
                Status = a != null && b != null ? "READY" : "PENDING"
            });
        }
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new(BracketError.None,
            new BracketGenerated(tournamentId, teams.Count, plan.Matches.Count, plan.Matches.Count, options.GrandFinalReset));
    }

    // ------------------------------------------------------------------ chuyển đội

    private sealed record Ctx(BracketPlan Plan, PlannedMatch Current, Func<PlannedMatch, Match?> Resolve,
        int TeamA, int TeamB, int Winner);

    /// <summary>Nạp kế hoạch nhánh và trận hiện tại; trận phải đã có kết quả cuối (VERIFIED / WALKOVER) với đội thắng.</summary>
    private async Task<(Ctx? Ctx, BracketError Error)> PrepareAsync(int matchId, CancellationToken ct)
    {
        var match = await _db.Set<Match>().FirstOrDefaultAsync(m => m.MatchId == matchId, ct);
        if (match == null) return (null, BracketError.MatchNotFound);
        if (match.BracketNodeId == null) return (null, BracketError.NotInBracket);

        var nodes = await _db.Set<BracketNode>().Where(n => n.TournamentId == match.TournamentId).ToListAsync(ct);
        nodes = nodes.Where(n => IsBracketLabel(n.GroupLabel)).ToList();
        if (!nodes.Any(n => n.BracketNodeId == match.BracketNodeId)) return (null, BracketError.NotInBracket);

        if (match.Status != "VERIFIED" && match.Status != "WALKOVER") return (null, BracketError.MatchNotFinished);
        if (match.WinnerTeamId == null || match.TeamAid == null || match.TeamBid == null)
            return (null, BracketError.MatchNotFinished);
        int teamA = Convert.ToInt32(match.TeamAid), teamB = Convert.ToInt32(match.TeamBid);
        int winner = Convert.ToInt32(match.WinnerTeamId);
        if (winner != teamA && winner != teamB) return (null, BracketError.MatchNotFinished);

        // Khôi phục kế hoạch từ số nút: không tính trận reset thì luôn có đúng 2n-2 trận.
        bool hasReset = nodes.Any(n => n.GroupLabel == LabelGrand && n.RoundNo == 2);
        int nonReset = nodes.Count - (hasReset ? 1 : 0);
        int teamCount = (nonReset + 2) / 2;
        BracketPlan plan;
        try { plan = DoubleEliminationPlanner.Build(teamCount, hasReset); }
        catch (ArgumentOutOfRangeException) { return (null, BracketError.NotInBracket); }
        if (plan.Matches.Count != nodes.Count) return (null, BracketError.NotInBracket);

        var matches = await _db.Set<Match>().Where(m => m.TournamentId == match.TournamentId).ToListAsync(ct);
        var byNode = matches.Where(m => m.BracketNodeId != null)
            .ToDictionary(m => Convert.ToInt32(m.BracketNodeId));
        var nodeByKey = nodes.ToDictionary(n => (n.GroupLabel, n.RoundNo, n.PositionInRound));

        Match? Resolve(PlannedMatch pm)
        {
            if (!nodeByKey.TryGetValue((Label(pm.Side), pm.Round, pm.Position), out var n)) return null;
            return byNode.TryGetValue(n.BracketNodeId, out var m) ? m : null;
        }

        var current = plan.Matches.FirstOrDefault(pm => Resolve(pm)?.MatchId == matchId);
        if (current == null) return (null, BracketError.NotInBracket);
        return (new Ctx(plan, current, Resolve, teamA, teamB, winner), BracketError.None);
    }

    public async Task<BracketAdvanceResult> AdvanceAsync(int matchId, CancellationToken ct = default)
    {
        var (c, error) = await PrepareAsync(matchId, ct);
        if (c == null) return new(error);
        var plan = c.Plan;
        var outcome = DoubleEliminationEngine.Advance(plan, c.Current.No, c.TeamA, c.TeamB,
            c.Winner == c.TeamA ? Slot.A : Slot.B);

        // Kiểm tra trước khi ghi: trận đích phải chưa bắt đầu.
        var targets = new List<(Match Target, Slot Slot, int TeamId)>();
        foreach (var p in outcome.Placements)
        {
            var target = c.Resolve(plan.Get(p.MatchNo));
            if (target == null) return new(BracketError.NotInBracket);
            bool isReset = plan.ResetNo == p.MatchNo;
            if (!Editable(target.Status) && !(isReset && target.Status == "CANCELLED"))
                return new(BracketError.DownstreamStarted);
            targets.Add((target, p.Slot, p.TeamId));
        }
        Match? resetMatch = plan.ResetNo is int rn ? c.Resolve(plan.Get(rn)) : null;
        if (outcome.ResetVoided && resetMatch != null && !Editable(resetMatch.Status) && resetMatch.Status != "CANCELLED")
            return new(BracketError.DownstreamStarted);

        foreach (var (target, slot, teamId) in targets)
        {
            if (slot == Slot.A) target.TeamAid = teamId; else target.TeamBid = teamId;
            if (target.TeamAid != null && target.TeamBid != null
                && (target.Status == "PENDING" || target.Status == "CANCELLED"))
                target.Status = "READY";
        }
        if (outcome.ResetVoided && resetMatch != null)
        {
            resetMatch.TeamAid = null;
            resetMatch.TeamBid = null;
            resetMatch.Status = "CANCELLED";
        }
        await _db.SaveChangesAsync(ct);

        var infos = targets.Select(x => new BracketPlacementInfo(x.Target.MatchId, x.Slot.ToString(), x.TeamId)).ToList();
        return new(BracketError.None, new BracketAdvanced(matchId, infos, outcome.EliminatedTeamId,
            outcome.ResetActivated, outcome.ResetVoided, outcome.ChampionTeamId));
    }

    /// <summary>
    /// Hoàn tác việc đưa đội đi tiếp của một trận (khi kết quả bị gỡ, vd BTC gỡ cờ No-show). Chỉ khi mọi trận đích chưa bắt đầu.
    /// Không mở giao dịch riêng để người gọi gộp chung với việc sửa chính trận đó.
    /// </summary>
    public async Task<BracketError> RollbackAsync(int matchId, CancellationToken ct = default)
    {
        var (c, error) = await PrepareAsync(matchId, ct);
        if (c == null) return error;
        var plan = c.Plan;
        var outcome = DoubleEliminationEngine.Advance(plan, c.Current.No, c.TeamA, c.TeamB,
            c.Winner == c.TeamA ? Slot.A : Slot.B);

        var targets = new List<(Match Target, Slot Slot, int TeamId, bool IsReset)>();
        foreach (var p in outcome.Placements)
        {
            var target = c.Resolve(plan.Get(p.MatchNo));
            if (target == null) return BracketError.NotInBracket;
            bool isReset = plan.ResetNo == p.MatchNo;
            if (!Editable(target.Status)) return BracketError.DownstreamStarted;
            targets.Add((target, p.Slot, p.TeamId, isReset));
        }
        Match? resetMatch = plan.ResetNo is int rn ? c.Resolve(plan.Get(rn)) : null;
        if (outcome.ResetVoided && resetMatch != null && !Editable(resetMatch.Status) && resetMatch.Status != "CANCELLED")
            return BracketError.DownstreamStarted;

        foreach (var (target, slot, teamId, _) in targets)
        {
            // Chỉ gỡ ô đang giữ đúng đội đã được đưa xuống
            if (slot == Slot.A && target.TeamAid == teamId) target.TeamAid = null;
            if (slot == Slot.B && target.TeamBid == teamId) target.TeamBid = null;
            if (target.TeamAid == null || target.TeamBid == null) target.Status = "PENDING";
        }
        // Reset từng bị hủy vì GF1 đã có kết quả → trở lại chờ
        if (outcome.ResetVoided && resetMatch != null && resetMatch.Status == "CANCELLED")
            resetMatch.Status = "PENDING";
        await _db.SaveChangesAsync(ct);
        return BracketError.None;
    }

    private static bool IsBracketLabel(string? l) => l == LabelUpper || l == LabelLower || l == LabelGrand;
}