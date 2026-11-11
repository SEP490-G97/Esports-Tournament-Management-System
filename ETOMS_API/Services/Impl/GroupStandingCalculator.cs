namespace ETOMS_API.Services.Impl;

// Tính bảng xếp hạng một bảng đấu từ kết quả các trận vòng bảng (dùng cho BRK-08, sau này cả BRK-13).
// Điểm theo view vw_GroupStanding của database: thắng 3, thua 0, không có hòa.
// Bằng điểm thì áp tiêu chí phân định theo đúng thứ tự Ban tổ chức đã lưu ở Tournament.TieBreakerOrder (BRK-07).
// Hệ thống không tự bốc thăm: còn bằng nhau sau mọi tiêu chí, hoặc tới tiêu chí "Bốc thăm",
// thì để nguyên là hòa và chờ Ban tổ chức quyết định (UC-18 A2).
public static class GroupStandingCalculator
{
    public const string HeadToHead = "Đối đầu trực tiếp";
    public const string GameDifference = "Hiệu số ván";
    public const string GamesWon = "Tổng ván thắng";
    public const string DrawLots = "Bốc thăm";

    public static readonly IReadOnlyList<string> DefaultOrder = new[] { HeadToHead, GameDifference, GamesWon, DrawLots };

    public sealed record GroupMatch(int? TeamAId, int? TeamBId, int? ScoreA, int? ScoreB, int? WinnerTeamId, string Status);

    public sealed class Row
    {
        public int TeamId { get; init; }
        public int Played { get; set; }
        public int Won { get; set; }
        public int Lost { get; set; }
        public int GamesWon { get; set; }
        public int GamesLost { get; set; }
        public int Diff => GamesWon - GamesLost;
        public int Points => Won * 3;
        public int Position { get; set; }
        // Các đội cùng TieKey là chưa phân định được thứ hạng với nhau.
        public string? TieKey { get; set; }
        // Tiêu chí đã tách đội này khỏi các đội bằng điểm (BRK-13: "Tie-breaker đã áp dụng").
        public string? DecidedBy { get; set; }
    }

    public const string OrganizerDecision = "Ban tổ chức xếp";

    public static bool IsDecided(GroupMatch m) => m.Status is "VERIFIED" or "WALKOVER" && m.WinnerTeamId is not null;

    // Trận đã xong: có kết quả, hoặc đã hủy (không tính điểm).
    public static bool IsFinished(GroupMatch m) => m.Status is "VERIFIED" or "WALKOVER" or "CANCELLED";

    public static List<string> ParseOrder(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored)) return DefaultOrder.ToList();
        return stored.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }

    // decidedOrder: thứ tự Ban tổ chức chọn cho những đội đang hòa (mỗi danh sách là một nhóm hòa).
    public static List<Row> Compute(IEnumerable<GroupMatch> matches, IReadOnlyList<string> order, IEnumerable<IReadOnlyList<int>>? decidedOrder = null, string tiePrefix = "")
    {
        var list = matches.ToList();
        var rows = new Dictionary<int, Row>();
        Row RowOf(int id) => rows.TryGetValue(id, out var r) ? r : rows[id] = new Row { TeamId = id };

        foreach (var m in list)
        {
            if (m.TeamAId is int a) RowOf(a);
            if (m.TeamBId is int b) RowOf(b);
        }

        var decided = list.Where(m => IsDecided(m) && m.TeamAId.HasValue && m.TeamBId.HasValue).ToList();
        foreach (var m in decided)
        {
            var ra = RowOf(m.TeamAId!.Value);
            var rb = RowOf(m.TeamBId!.Value);
            ra.Played++; rb.Played++;
            ra.GamesWon += m.ScoreA ?? 0; ra.GamesLost += m.ScoreB ?? 0;
            rb.GamesWon += m.ScoreB ?? 0; rb.GamesLost += m.ScoreA ?? 0;
            if (m.WinnerTeamId == ra.TeamId) { ra.Won++; rb.Lost++; }
            else if (m.WinnerTeamId == rb.TeamId) { rb.Won++; ra.Lost++; }
        }

        var blocks = new List<List<Row>>();
        foreach (var byPoints in rows.Values.GroupBy(r => r.Points).OrderByDescending(g => g.Key))
            blocks.AddRange(Separate(byPoints.ToList(), order, 0, decided, null));

        // Áp quyết định của Ban tổ chức cho nhóm hòa khớp đúng tập đội.
        foreach (var decision in decidedOrder ?? Enumerable.Empty<IReadOnlyList<int>>())
        {
            var set = decision.ToHashSet();
            var i = blocks.FindIndex(b => b.Count > 1 && b.Count == set.Count && b.All(r => set.Contains(r.TeamId)));
            if (i < 0) continue;
            var byId = blocks[i].ToDictionary(r => r.TeamId);
            blocks.RemoveAt(i);
            foreach (var r in byId.Values) r.DecidedBy = OrganizerDecision;
            blocks.InsertRange(i, decision.Select(id => new List<Row> { byId[id] }));
        }

        var result = new List<Row>();
        var position = 1;
        var tieNo = 0;
        foreach (var block in blocks)
        {
            var key = block.Count > 1 ? tiePrefix + (++tieNo) : null;
            foreach (var r in block.OrderBy(r => r.TeamId))
            {
                r.Position = position;
                r.TieKey = key;
                result.Add(r);
            }
            position += block.Count;
        }
        return result;
    }

    // Chia một nhóm bằng điểm thành các khối theo từng tiêu chí; khối còn nhiều đội là hòa.
    // decidedBy: tiêu chí vừa tách ra nhóm này (null khi nhóm đến thẳng từ điểm số).
    private static IEnumerable<List<Row>> Separate(List<Row> tied, IReadOnlyList<string> order, int index, List<GroupMatch> decided, string? decidedBy)
    {
        if (tied.Count == 1)
        {
            tied[0].DecidedBy = decidedBy;
            yield return tied;
            yield break;
        }
        if (index >= order.Count)
        {
            yield return tied;
            yield break;
        }

        var criterion = order[index];
        if (criterion == DrawLots)
        {
            yield return tied;
            yield break;
        }

        Func<Row, int>? value = criterion switch
        {
            HeadToHead => HeadToHeadPoints(tied, decided),
            GameDifference => r => r.Diff,
            GamesWon => r => r.GamesWon,
            _ => null
        };
        if (value is null)
        {
            foreach (var b in Separate(tied, order, index + 1, decided, decidedBy)) yield return b;
            yield break;
        }

        foreach (var sub in tied.GroupBy(value).OrderByDescending(g => g.Key))
            foreach (var b in Separate(sub.ToList(), order, index + 1, decided, criterion))
                yield return b;
    }

    // Điểm chỉ tính các trận giữa những đội đang bằng điểm với nhau.
    private static Func<Row, int> HeadToHeadPoints(List<Row> tied, List<GroupMatch> decided)
    {
        var ids = tied.Select(r => r.TeamId).ToHashSet();
        var points = tied.ToDictionary(r => r.TeamId, _ => 0);
        foreach (var m in decided.Where(m => ids.Contains(m.TeamAId!.Value) && ids.Contains(m.TeamBId!.Value)))
            points[m.WinnerTeamId!.Value] += 3;
        return r => points[r.TeamId];
    }
}
