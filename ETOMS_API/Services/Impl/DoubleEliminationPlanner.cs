namespace ETOMS_API.Services.Impl;

public enum BracketSide { Upper, Lower, Grand }

public enum Slot { A, B }

public enum SourceKind
{
    /// <summary>Chưa có nguồn (chỉ dùng nội bộ; không xuất hiện trong kế hoạch cuối).</summary>
    Empty,
    /// <summary>Hạt giống cố định (Ref = số hạt giống, 1 = mạnh nhất).</summary>
    Seed,
    /// <summary>Đội thắng của trận số Ref.</summary>
    Winner,
    /// <summary>Đội thua của trận số Ref.</summary>
    Loser
}

public readonly record struct Source(SourceKind Kind, int Ref);

public readonly record struct Route(int MatchNo, Slot Slot);

public sealed class PlannedMatch
{
    public int No { get; init; }
    public BracketSide Side { get; init; }
    /// <summary>Vòng đã nén liên tục từ 1 trong từng nhánh (Upper/Lower); Grand: 1 = GF, 2 = trận reset.</summary>
    public int Round { get; init; }
    public int Position { get; init; }
    public Source A { get; init; }
    public Source B { get; init; }
    /// <summary>Đội thắng đi tiếp tới đâu (null với Grand Final).</summary>
    public Route? WinnerTo { get; internal set; }
    /// <summary>Đội thua rớt xuống đâu (null nếu bị loại hẳn).</summary>
    public Route? LoserTo { get; internal set; }
    /// <summary>Trận Bracket Reset (GF2): chỉ được đấu nếu đội nhánh thua thắng GF1.</summary>
    public bool IsReset { get; init; }
}

public sealed class BracketPlan
{
    public int TeamCount { get; init; }
    /// <summary>Kích thước bracket lý thuyết (lũy thừa của 2 ≥ TeamCount).</summary>
    public int Size { get; init; }
    public IReadOnlyList<PlannedMatch> Matches { get; init; } = Array.Empty<PlannedMatch>();
    public int GrandFinalNo { get; init; }
    public int? ResetNo { get; init; }

    public PlannedMatch Get(int no) => Matches[no - 1];
}

/// <summary>
/// BRK-05 — Sinh khung nhánh Double Elimination (thuần tính toán, không phụ thuộc DB).
/// Upper: loại trực tiếp chuẩn. Lower: vòng lẻ chỉ đấu giữa các đội Lower, vòng chẵn nhận đội thua từ Upper
/// vòng tương ứng; vị trí rớt được đảo ngược ở các vòng chẵn lẻ xen kẽ để hạn chế gặp lại đối thủ cũ.
/// Số đội không phải lũy thừa của 2: các suất trống (bye) được "nén" — trận có một phía trống không tạo ra,
/// đội còn lại đi thẳng tới vòng sau; không có đội thua thì không có ai rớt xuống Lower.
/// </summary>
public static class DoubleEliminationPlanner
{
    public const int MinTeams = 3;
    public const int MaxTeams = 128;

    private readonly record struct Tid(char Side, int Round, int Pos);

    // Nguồn trung gian khi còn dùng Tid (trước khi đánh số trận).
    private readonly record struct TSrc(SourceKind Kind, int Seed, Tid Ref);

    public static BracketPlan Build(int teamCount, bool grandFinalReset = true)
    {
        if (teamCount < MinTeams || teamCount > MaxTeams)
            throw new ArgumentOutOfRangeException(nameof(teamCount), $"Cần từ {MinTeams} đến {MaxTeams} đội.");

        int k = 1;
        while ((1 << k) < teamCount) k++;
        int size = 1 << k;
        var order = SeedOrder(size);

        var T = new Dictionary<Tid, (TSrc A, TSrc B)>();
        void Add(char s, int r, int i, TSrc a, TSrc b) => T[new Tid(s, r, i)] = (a, b);
        TSrc Seed(int n) => new(SourceKind.Seed, n, default);
        TSrc Win(char s, int r, int i) => new(SourceKind.Winner, 0, new Tid(s, r, i));
        TSrc Lose(char s, int r, int i) => new(SourceKind.Loser, 0, new Tid(s, r, i));

        // Upper
        for (int r = 1; r <= k; r++)
            for (int i = 1; i <= size >> r; i++)
                if (r == 1) Add('U', r, i, Seed(order[2 * i - 2]), Seed(order[2 * i - 1]));
                else Add('U', r, i, Win('U', r - 1, 2 * i - 1), Win('U', r - 1, 2 * i));

        // Lower
        for (int i = 1; i <= size / 4; i++) Add('L', 1, i, Lose('U', 1, 2 * i - 1), Lose('U', 1, 2 * i));
        for (int j = 1; j < k; j++)
        {
            int m = size >> (j + 1);
            for (int i = 1; i <= m; i++)
            {
                int idx = j % 2 == 1 ? m + 1 - i : i;   // đảo vị trí rớt xuống ở vòng lẻ của Upper
                Add('L', 2 * j, i, Win('L', 2 * j - 1, i), Lose('U', j + 1, idx));
            }
            if (j <= k - 2)
                for (int i = 1; i <= size >> (j + 2); i++)
                    Add('L', 2 * j + 1, i, Win('L', 2 * j, 2 * i - 1), Win('L', 2 * j, 2 * i));
        }
        var ubFinal = new Tid('U', k, 1);
        var lbFinal = new Tid('L', 2 * (k - 1), 1);

        // Giải bye bằng memo: Empty / Seed / Winner(tid) / Loser(tid)
        var memoW = new Dictionary<Tid, TSrc>();
        var memoL = new Dictionary<Tid, TSrc>();
        var empty = new TSrc(SourceKind.Empty, 0, default);

        TSrc Res(TSrc s) => s.Kind switch
        {
            SourceKind.Seed => s.Seed <= teamCount ? s : empty,
            SourceKind.Winner => OutW(s.Ref),
            SourceKind.Loser => OutL(s.Ref),
            _ => empty
        };
        (TSrc, TSrc) Inputs(Tid t) => (Res(T[t].A), Res(T[t].B));
        TSrc OutW(Tid t)
        {
            if (memoW.TryGetValue(t, out var c)) return c;
            var (a, b) = Inputs(t);
            TSrc r = a.Kind == SourceKind.Empty && b.Kind == SourceKind.Empty ? empty
                   : a.Kind == SourceKind.Empty ? b
                   : b.Kind == SourceKind.Empty ? a
                   : new TSrc(SourceKind.Winner, 0, t);
            return memoW[t] = r;
        }
        TSrc OutL(Tid t)
        {
            if (memoL.TryGetValue(t, out var c)) return c;
            var (a, b) = Inputs(t);
            TSrc r = a.Kind != SourceKind.Empty && b.Kind != SourceKind.Empty
                ? new TSrc(SourceKind.Loser, 0, t) : empty;
            return memoL[t] = r;
        }

        var real = T.Keys
            .Where(t => { var (a, b) = Inputs(t); return a.Kind != SourceKind.Empty && b.Kind != SourceKind.Empty; })
            .OrderBy(t => t.Side == 'U' ? 0 : 1).ThenBy(t => t.Round).ThenBy(t => t.Pos)
            .ToList();

        // Nén vòng / vị trí theo từng nhánh
        var disp = new Dictionary<Tid, (int Round, int Pos)>();
        foreach (var side in new[] { 'U', 'L' })
        {
            var rounds = real.Where(x => x.Side == side).Select(x => x.Round).Distinct().OrderBy(x => x).ToList();
            for (int ri = 0; ri < rounds.Count; ri++)
            {
                int pi = 1;
                foreach (var tid in real.Where(x => x.Side == side && x.Round == rounds[ri]))
                    disp[tid] = (ri + 1, pi++);
            }
        }
        var no = new Dictionary<Tid, int>();
        for (int i = 0; i < real.Count; i++) no[real[i]] = i + 1;

        Source Final(TSrc s) => s.Kind switch
        {
            SourceKind.Seed => new Source(SourceKind.Seed, s.Seed),
            SourceKind.Winner => new Source(SourceKind.Winner, no[s.Ref]),
            SourceKind.Loser => new Source(SourceKind.Loser, no[s.Ref]),
            _ => throw new InvalidOperationException("Nguồn trống còn sót lại trong kế hoạch.")
        };

        var list = new List<PlannedMatch>();
        foreach (var t in real)
        {
            var (a, b) = Inputs(t);
            list.Add(new PlannedMatch
            {
                No = no[t],
                Side = t.Side == 'U' ? BracketSide.Upper : BracketSide.Lower,
                Round = disp[t].Round,
                Position = disp[t].Pos,
                A = Final(a),
                B = Final(b)
            });
        }
        int gf1 = real.Count + 1;
        var ua = Final(OutW(ubFinal));
        var lb = Final(OutW(lbFinal));
        list.Add(new PlannedMatch { No = gf1, Side = BracketSide.Grand, Round = 1, Position = 1, A = ua, B = lb });
        int? gf2 = null;
        if (grandFinalReset)
        {
            gf2 = gf1 + 1;
            list.Add(new PlannedMatch { No = gf2.Value, Side = BracketSide.Grand, Round = 2, Position = 1, A = ua, B = lb, IsReset = true });
        }

        // Đường đi: bỏ qua trận reset (đội vào reset do engine quyết định, không theo nguồn).
        foreach (var m in list)
        {
            if (m.IsReset) continue;
            Link(m.A, Slot.A); Link(m.B, Slot.B);
            void Link(Source s, Slot slot)
            {
                if (s.Kind == SourceKind.Winner) list[s.Ref - 1].WinnerTo = new Route(m.No, slot);
                else if (s.Kind == SourceKind.Loser) list[s.Ref - 1].LoserTo = new Route(m.No, slot);
            }
        }

        return new BracketPlan { TeamCount = teamCount, Size = size, Matches = list, GrandFinalNo = gf1, ResetNo = gf2 };
    }

    /// <summary>Thứ tự hạt giống chuẩn: 1 gặp size, 2 gặp size-1... và hạt 1, 2 chỉ gặp nhau ở chung kết.</summary>
    public static List<int> SeedOrder(int size)
    {
        var o = new List<int> { 1 };
        while (o.Count < size)
        {
            int s = o.Count * 2;
            o = o.SelectMany(x => new[] { x, s + 1 - x }).ToList();
        }
        return o;
    }
}