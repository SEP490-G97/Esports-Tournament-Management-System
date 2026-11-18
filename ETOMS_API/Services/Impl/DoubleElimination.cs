using ETOMS_API.Models;

namespace ETOMS_API.Services.Impl;

// BRK-05 (NUI-BRK-04): Loại kép (Double Elimination). Không đổi lược đồ — mỗi BracketNode vẫn là một trận
// KNOCKOUT, ba nhánh phân biệt bằng StageNo:
//   1 = Nhánh thắng (winners bracket)  — cây chuẩn như loại trực tiếp, vòng 1..k (k = log2 kích thước nhánh)
//   2 = Nhánh thua  (losers bracket)   — vòng 1..2(k-1): vòng lẻ đấu nội bộ, vòng chẵn nhận đội thua rơi từ nhánh thắng
//   3 = Chung kết tổng (grand final)   — vô địch nhánh thắng gặp vô địch nhánh thua, một trận (không có trận "reset")
// GroupLabel để trống: các trang khác coi GroupLabel là tên bảng của vòng bảng.
//
// ParentNodeId chỉ nối đường đi của ĐỘI THẮNG (giống loại trực tiếp). Đường rơi của ĐỘI THUA tính xác định từ
// vị trí, nên sinh nhánh, đẩy đội sau khi có kết quả, nhãn trận và lịch đều dùng chung lớp này.
// Nhánh thua bỏ các ô không thể có đội (do đội miễn vòng 1 ở nhánh thắng không có đội thua):
//   - nhánh thua vòng 1, vị trí p chỉ có khi CẢ HAI trận 2p-1, 2p vòng 1 nhánh thắng là trận thật;
//     chỉ một trận thật thì đội thua đó vào thẳng nhánh thua vòng 2, ô A.
//   - nhánh thua vòng 2, vị trí p không có khi cả hai trận nguồn đều là miễn đấu; đội thua nhánh thắng vòng 2
//     khi đó vào thẳng nhánh thua vòng 3.
public static class DoubleElimination
{
    public const string Format = "DOUBLE_ELIMINATION";
    public const int WinnersStage = 1, LosersStage = 2, GrandFinalStage = 3;
    public const int MinTeams = 3;

    public static bool IsDoubleElimination(string? format) => format == Format;

    public sealed record Slot(BracketNode Node, bool SideA);

    // Số trận của vòng `round` nhánh thua khi nhánh thắng có `size` suất (size = 2^k).
    public static int LosersRoundCount(int size, int round) => size >> ((round + 1) / 2 + 1);

    // Vòng nhánh thua -> vòng nhánh thắng tương ứng để lấy số ván (BO): L1 = V1, L2-L3 = V2, L4-L5 = V3...
    public static int LosersBestOfRound(int winnersRounds, int losersRound) => Math.Min(winnersRounds, losersRound / 2 + 1);

    // Đội thua vòng r >= 2 của nhánh thắng rơi vào vị trí nào ở nhánh thua (đảo chiều ở vòng chẵn để
    // hạn chế hai đội vừa gặp nhau ở nhánh thắng gặp lại ngay).
    public static int DropPosition(int winnersRounds, int round, int position)
    {
        var count = 1 << (winnersRounds - round);
        return round % 2 == 0 ? count + 1 - position : position;
    }

    // Nhánh thua vòng 1/2 có tồn tại không, theo các trận miễn đấu ở vòng 1 nhánh thắng (byeR1[p-1] = trận p là miễn đấu).
    public static bool LosersNodeExists(int round, int position, IReadOnlyList<bool> byeR1)
    {
        if (round > 2) return true;
        bool a = byeR1[2 * position - 2], b = byeR1[2 * position - 1];
        return round == 1 ? !a && !b : !(a && b);
    }

    public sealed class Layout
    {
        private readonly Dictionary<(int Stage, int Round, int Pos), BracketNode> _map = new();
        public int WinnersRounds { get; }
        public int LosersRounds => 2 * (WinnersRounds - 1);
        public IReadOnlyCollection<BracketNode> Nodes => _map.Values;

        public Layout(IEnumerable<BracketNode> nodes)
        {
            foreach (var n in nodes.Where(n => n.StageType == "KNOCKOUT" && n.StageNo is >= WinnersStage and <= GrandFinalStage))
                _map[(n.StageNo, n.RoundNo ?? 0, n.PositionInRound ?? 0)] = n;
            WinnersRounds = _map.Keys.Where(k => k.Stage == WinnersStage).Select(k => k.Round).DefaultIfEmpty(0).Max();
        }

        public BracketNode? Get(int stage, int round, int pos) => _map.TryGetValue((stage, round, pos), out var n) ? n : null;

        public BracketNode? GrandFinal => Get(GrandFinalStage, 1, 1);

        private static int Half(int pos) => (pos + 1) / 2;
        private static bool Odd(int pos) => pos % 2 == 1;

        // Đội thắng của trận n đi đâu (null: chung kết tổng — hết nhánh).
        public Slot? WinnerTo(BracketNode n)
        {
            int r = n.RoundNo ?? 0, p = n.PositionInRound ?? 0;
            BracketNode? target; bool sideA;
            switch (n.StageNo)
            {
                case WinnersStage when r < WinnersRounds:
                    target = Get(WinnersStage, r + 1, Half(p)); sideA = Odd(p); break;
                case WinnersStage:
                    target = GrandFinal; sideA = true; break;
                case LosersStage when r == LosersRounds:
                    target = GrandFinal; sideA = false; break;
                case LosersStage when r % 2 == 1:
                    target = Get(LosersStage, r + 1, p); sideA = true; break;
                case LosersStage:
                    target = Get(LosersStage, r + 1, Half(p)); sideA = Odd(p); break;
                default:
                    return null;
            }
            return target is null ? null : new Slot(target, sideA);
        }

        // Đội thua của trận nhánh thắng rơi xuống đâu ở nhánh thua (null: thua ở nhánh thua/chung kết tổng là bị loại).
        public Slot? LoserTo(BracketNode n)
        {
            if (n.StageNo != WinnersStage || WinnersRounds < 2) return null;
            int r = n.RoundNo ?? 0, p = n.PositionInRound ?? 0;
            if (r == 1)
            {
                var first = Get(LosersStage, 1, Half(p));
                if (first is not null) return new Slot(first, Odd(p));
                var second = Get(LosersStage, 2, Half(p));
                return second is null ? null : new Slot(second, true);
            }
            var q = DropPosition(WinnersRounds, r, p);
            var drop = Get(LosersStage, 2 * (r - 1), q);
            if (drop is not null) return new Slot(drop, false);
            var next = Get(LosersStage, 2 * r - 1, Half(q));
            return next is null ? null : new Slot(next, Odd(q));
        }

        // Trận nào đổ đội vào ô (target, sideA): (trận nguồn, true = đội thua của trận đó).
        public (BracketNode From, bool Loser)? Feeder(BracketNode target, bool sideA)
        {
            foreach (var n in _map.Values)
            {
                if (WinnerTo(n) is { } w && w.Node == target && w.SideA == sideA) return (n, false);
                if (LoserTo(n) is { } l && l.Node == target && l.SideA == sideA) return (n, true);
            }
            return null;
        }

        public IEnumerable<BracketNode> Feeders(BracketNode target)
            => _map.Values.Where(n => WinnerTo(n)?.Node == target || LoserTo(n)?.Node == target);

        public IEnumerable<BracketNode> Dependents(BracketNode source)
        {
            if (WinnerTo(source) is { } w) yield return w.Node;
            if (LoserTo(source) is { } l) yield return l.Node;
        }

        public string RoundLabel(int stage, int round) => stage switch
        {
            WinnersStage when round == WinnersRounds => "Chung kết nhánh thắng",
            WinnersStage => "Nhánh thắng · " + BracketLabels.KnockoutRound(WinnersRounds, round),
            LosersStage when round == LosersRounds => "Chung kết nhánh thua",
            LosersStage => "Nhánh thua · Vòng " + round,
            _ => "Chung kết tổng"
        };

        public string MatchLabel(BracketNode n)
        {
            var round = RoundLabel(n.StageNo, n.RoundNo ?? 0);
            return ManyInRound(n) ? $"{round} · Trận {n.PositionInRound}" : round;
        }

        // Nhãn ngắn khi đã nằm trong khối của nhánh (tiêu đề khối đã ghi "Nhánh thắng"/"Nhánh thua").
        public string ShortRoundLabel(int stage, int round) => stage switch
        {
            WinnersStage => BracketLabels.KnockoutRound(WinnersRounds, round),
            LosersStage when round == LosersRounds => "Chung kết",
            LosersStage => "Vòng " + round,
            _ => "Chung kết tổng"
        };

        public string ShortMatchLabel(BracketNode n)
        {
            var round = ShortRoundLabel(n.StageNo, n.RoundNo ?? 0);
            return ManyInRound(n) ? $"{round} · Trận {n.PositionInRound}" : round;
        }

        private bool ManyInRound(BracketNode n) => _map.Keys.Count(k => k.Stage == n.StageNo && k.Round == (n.RoundNo ?? 0)) > 1;

        // Vòng (theo nhánh thắng) để tra số ván: nhánh thua theo LosersBestOfRound, chung kết tổng = chung kết.
        public int BestOfRound(BracketNode n) => n.StageNo switch
        {
            WinnersStage => n.RoundNo ?? 1,
            LosersStage => LosersBestOfRound(WinnersRounds, n.RoundNo ?? 1),
            _ => WinnersRounds
        };
    }

    public static string StageLabel(int stage) => stage switch
    {
        WinnersStage => "Nhánh thắng",
        LosersStage => "Nhánh thua",
        _ => "Chung kết tổng"
    };
}
