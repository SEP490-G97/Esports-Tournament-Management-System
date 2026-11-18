using ETOMS_API.Services.Impl;

namespace ETOMS_API.Utils;

// Vòng đời giải (mục 28, góp ý nhóm 18/11): trạng thái chuyển theo VIỆC THẬT, không theo giờ.
//   PUBLISHED -> ONGOING   : nhánh đã khóa và tới giờ khai mạc (tự động), hoặc BTC bấm "Bắt đầu giải" khi nhánh đã khóa.
//   ONGOING   -> COMPLETED : mọi trận đã xong và xác định được hạng chung cuộc (tự động), hoặc BTC bấm "Kết thúc giải".
// "Chưa đủ đội": đã đóng đăng ký, chưa khóa nhánh, số đội được duyệt dưới mức tối thiểu để sinh nhánh.
// File này chỉ tính toán (không đọc DB) để kiểm thử được.
public static class TournamentLifecycle
{
    public const string ShortOfTeamsLabel = "Chưa đủ đội";

    // Cùng mức với BracketService.GenerateBracketAsync: 2 đội; Loại kép cần 3 đội mới có nhánh thua thật.
    public static int MinTeams(string? format) => format == "DOUBLE_ELIMINATION" ? 3 : 2;

    public static bool IsRegistrationOpen(string status, bool bracketLocked, DateTime regOpenAt, DateTime regCloseAt, DateTime now)
        => status == "PUBLISHED" && !bracketLocked && regOpenAt <= now && now < regCloseAt;

    public static bool IsShortOfTeams(string status, bool bracketLocked, DateTime regCloseAt, DateTime now, int approved, string? format)
        => status == "PUBLISHED" && !bracketLocked && now >= regCloseAt && approved < MinTeams(format);

    // Nhãn công khai (danh sách giải, trang giải, trang chủ).
    public static (string Label, string Class) PublicLabel(string status, bool bracketLocked, DateTime regOpenAt, DateTime regCloseAt,
        DateTime now, int approved, string? format)
    {
        if (IsRegistrationOpen(status, bracketLocked, regOpenAt, regCloseAt, now)) return ("Đang mở đăng ký", "badge-active");
        if (IsShortOfTeams(status, bracketLocked, regCloseAt, now, approved, format)) return (ShortOfTeamsLabel, "badge-danger");
        if (status == "PUBLISHED") return ("Sắp diễn ra", "badge-active");
        return (StatusText.Tournament(status), StatusText.TournamentClass(status));
    }

    public static bool IsMatchFinished(string status) => status is "VERIFIED" or "WALKOVER" or "CANCELLED";

    public sealed record Node(int NodeId, string StageType, int StageNo, int? RoundNo, int? Position, int? ParentNodeId, bool IsBye);
    public sealed record MatchRow(int NodeId, int? TeamA, int? TeamB, int? Winner, string Status);
    // Một dòng bảng xếp hạng vòng bảng (Position: cùng số = đang bằng nhau).
    public sealed record GroupRow(string Group, int TeamId, int Position, int Points, int Diff, int GamesWon);

    public sealed class Outcome
    {
        // Lý do chưa kết thúc được (null = đủ điều kiện).
        public string? Blocked { get; init; }
        // Hạng chung cuộc theo TeamId (1 = vô địch). Có khi Blocked == null.
        public Dictionary<int, int> Ranks { get; init; } = new();
        // Các đội đang bằng nhau ở hạng 1–2 (chỉ thể thức vòng tròn) — BTC chọn thứ tự.
        public List<int> TiedTop { get; init; } = new();
    }

    // teamIds: các đội được duyệt (mọi đội đều nhận một hạng; đội không có trận nào xếp cuối).
    // tieOrder: thứ tự BTC chọn cho nhóm đang bằng nhau ở hạng 1–2 (null = chưa chọn).
    public static Outcome Evaluate(string format, bool hasThirdPlaceMatch, IReadOnlyList<int> teamIds,
        IReadOnlyList<Node> nodes, IReadOnlyList<MatchRow> matches, IReadOnlyList<GroupRow> groupRows, IReadOnlyList<int>? tieOrder)
    {
        if (nodes.Count == 0) return Blocked("Giải chưa có nhánh đấu.");
        if (matches.Count == 0) return Blocked("Giải chưa có trận đấu nào.");
        var left = matches.Count(m => !IsMatchFinished(m.Status));
        if (left > 0) return Blocked($"Còn {left} trận chưa có kết quả.");

        var ko = nodes.Where(n => n.StageType == "KNOCKOUT").ToList();
        var hasGroups = nodes.Any(n => n.StageType == "GROUP");
        if (format == "HYBRID" && ko.Count == 0) return Blocked("Vòng bảng đã xong nhưng chưa tạo nhánh knockout.");

        // Điểm "đi xa tới đâu" của từng đội: càng lớn càng xếp trên. Hạng = 1 + số đội có điểm lớn hơn hẳn.
        var key = new Dictionary<int, double>();
        void Raise(int? team, double k) { if (team is int t) key[t] = key.TryGetValue(t, out var old) ? Math.Max(old, k) : k; }
        var byNode = matches.GroupBy(m => m.NodeId).ToDictionary(g => g.Key, g => g.ToList());
        int? Loser(MatchRow m) => m.Winner is null ? null : m.Winner == m.TeamA ? m.TeamB : m.TeamA;

        if (hasGroups)
        {
            // Đội dừng ở vòng bảng: luôn dưới mọi đội vào knockout; so với nhau theo vị trí trong bảng.
            foreach (var r in groupRows) Raise(r.TeamId, -1000 - r.Position);
        }

        if (ko.Count > 0 && format == "DOUBLE_ELIMINATION")
        {
            var lbMax = ko.Where(n => n.StageNo == DoubleElimination.LosersStage).Select(n => n.RoundNo ?? 0).DefaultIfEmpty(0).Max();
            var gfNodes = ko.Where(n => n.StageNo == DoubleElimination.GrandFinalStage).OrderBy(n => n.RoundNo ?? 0).ToList();
            var gf = gfNodes.SelectMany(n => byNode.GetValueOrDefault(n.NodeId) ?? new()).Where(m => m.Winner is not null).LastOrDefault();
            if (gf is null) return Blocked("Chung kết tổng chưa có đội thắng.");
            foreach (var n in ko)
                foreach (var m in byNode.GetValueOrDefault(n.NodeId) ?? new())
                {
                    Raise(m.TeamA, 0); Raise(m.TeamB, 0);
                    if (n.StageNo == DoubleElimination.LosersStage) Raise(Loser(m), n.RoundNo ?? 0);
                }
            Raise(Loser(gf), lbMax + 1);
            Raise(gf.Winner, lbMax + 2);
        }
        else if (ko.Count > 0)
        {
            var stage = ko.Max(n => n.StageNo);
            var finalRound = ko.Where(n => n.StageNo == stage).Max(n => n.RoundNo ?? 0);
            var finalNode = ko.FirstOrDefault(n => n.StageNo == stage && n.RoundNo == finalRound && n.Position == 1 && n.ParentNodeId == null);
            var final = finalNode is null ? null : byNode.GetValueOrDefault(finalNode.NodeId)?.FirstOrDefault();
            if (final?.Winner is null) return Blocked("Trận chung kết chưa có đội thắng.");

            var thirdNode = ko.FirstOrDefault(n => n.StageNo == stage && n.RoundNo == finalRound && n.Position == 2 && n.ParentNodeId == null);
            var semis = ko.Count(n => n.ParentNodeId == finalNode!.NodeId && !n.IsBye);
            if (hasThirdPlaceMatch && semis == 2 && thirdNode is null) return Blocked("Chưa có trận tranh hạng Ba.");

            foreach (var n in ko.Where(n => n.StageNo == stage && n != thirdNode))
                foreach (var m in byNode.GetValueOrDefault(n.NodeId) ?? new())
                {
                    Raise(m.TeamA, 0); Raise(m.TeamB, 0);
                    Raise(Loser(m), n.RoundNo ?? 0);
                }
            var third = thirdNode is null ? null : byNode.GetValueOrDefault(thirdNode.NodeId)?.FirstOrDefault();
            if (third?.Winner is not null)
            {
                Raise(Loser(third), finalRound - 0.75);
                Raise(third.Winner, finalRound - 0.5);
            }
            Raise(Loser(final), finalRound);
            Raise(final.Winner, finalRound + 1);
        }
        else
        {
            // Vòng tròn một bảng: đúng thứ hạng của bảng (đã áp tiêu chí phân định của giải).
            // Nhiều bảng: so vị trí trong bảng trước, rồi điểm, hiệu số, số ván thắng.
            key.Clear();
            var single = groupRows.Select(r => r.Group).Distinct().Count() <= 1;
            foreach (var r in groupRows)
                key[r.TeamId] = single ? -r.Position : -r.Position * 1_000_000.0 + r.Points * 10_000.0 + r.Diff * 100.0 + r.GamesWon;
        }

        // Đội được duyệt nhưng không đá trận nào (vd bị loại khỏi nhánh): xếp cuối.
        foreach (var t in teamIds) if (!key.ContainsKey(t)) key[t] = double.MinValue;
        var ranks = teamIds.Distinct().ToDictionary(t => t, t => 1 + teamIds.Distinct().Count(o => key[o] > key[t]));

        // Hạng 1 và hạng 2 phải là MỘT đội (bảng xếp hạng chỉ ghi nhận vô địch / á quân khi duy nhất).
        var contested = ranks.Count(r => r.Value == 1) > 1 ? ranks.Where(r => r.Value == 1).Select(r => r.Key).ToList()
            : ranks.Count(r => r.Value == 2) > 1 ? ranks.Where(r => r.Value == 2).Select(r => r.Key).ToList()
            : new List<int>();
        if (contested.Count > 0)
        {
            var set = contested.ToHashSet();
            if (tieOrder is null || tieOrder.Count != set.Count || !tieOrder.All(set.Contains) || tieOrder.Distinct().Count() != set.Count)
                return new Outcome { Blocked = "Các đội đứng đầu đang bằng nhau mọi chỉ số. Ban tổ chức chọn thứ tự xếp hạng trước khi kết thúc giải.", TiedTop = contested.OrderBy(x => x).ToList() };
            var baseRank = ranks[contested[0]];
            for (var i = 0; i < tieOrder.Count; i++) ranks[tieOrder[i]] = baseRank + i;
        }
        return new Outcome { Ranks = ranks };
    }

    public static string Placement(int rank) => rank switch { 1 => "CHAMPION", 2 => "RUNNER_UP", 3 => "THIRD", _ => "PARTICIPANT" };

    private static Outcome Blocked(string why) => new() { Blocked = why };
}

