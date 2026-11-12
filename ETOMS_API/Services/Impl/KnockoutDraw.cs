namespace ETOMS_API.Services.Impl;

// BRK-08: xếp các đội đi tiếp từ vòng bảng vào vòng 1 của cây knockout.
// Hạt giống theo tầng: mọi đội nhất bảng trước, rồi mọi đội nhì bảng, ...
// Đội nhì được đặt sao cho Nhất bảng A gặp Nhì bảng B, Nhất B gặp Nhì A, Nhất C gặp Nhì D, ...
// và hai đội cùng bảng nằm ở hai nửa nhánh khác nhau. Các trường hợp còn lại (số bảng lẻ, hơn 2 đội
// đi tiếp mỗi bảng, số đội không tròn lũy thừa 2) xếp theo tầng, chia bye cho hạt giống cao nhất như
// BRK-01, rồi đổi chỗ để hai đội cùng bảng không gặp nhau ngay vòng 1 khi còn cách đổi được.
public static class KnockoutDraw
{
    public sealed record Qualifier(int TeamId, string GroupLabel, int Position)
    {
        public string Label => (Position == 1 ? "Nhất" : Position == 2 ? "Nhì" : "Hạng " + Position) + " bảng " + GroupLabel;
    }

    public sealed record Slot(Qualifier Team, int SeedNo);

    // groups: theo thứ tự nhãn bảng, mỗi bảng là danh sách đội đi tiếp đã xếp hạng.
    // Kết quả: danh sách vị trí vòng 1 (độ dài lũy thừa 2), null là bye.
    public static List<Slot?> Build(IReadOnlyList<IReadOnlyList<Qualifier>> groups)
    {
        var seeds = SeedList(groups);
        var size = NextPowerOfTwo(seeds.Count);
        var order = StandardSeedOrder(size);
        var slots = order.Select(seedNo => seedNo <= seeds.Count ? new Slot(seeds[seedNo - 1], seedNo) : null).ToList();
        AvoidSameGroup(slots);
        return slots;
    }

    private static List<Qualifier> SeedList(IReadOnlyList<IReadOnlyList<Qualifier>> groups)
    {
        var g = groups.Count;
        var tiers = groups.Count == 0 ? 0 : groups.Max(x => x.Count);
        var seeds = new List<Qualifier>();
        for (var t = 0; t < tiers; t++)
        {
            var tier = groups.Where(x => x.Count > t).Select(x => x[t]).ToList();
            var pairedRunnersUp = t == 1 && g % 2 == 0 && tier.Count == g && groups.All(x => x.Count == 2);
            if (pairedRunnersUp)
            {
                // Hạt giống s gặp hạt giống 2g+1-s ở vòng 1. Nhì bảng X đứng ở vị trí đối diện Nhất của bảng cặp với X.
                var placed = new Qualifier[g];
                for (var i = 0; i < g; i++)
                    placed[(g - 1) - (i ^ 1)] = tier[i];
                seeds.AddRange(placed);
            }
            else
            {
                seeds.AddRange(tier);
            }
        }
        return seeds;
    }

    private static void AvoidSameGroup(List<Slot?> slots)
    {
        bool Clash(int pair) => slots[2 * pair] is { } a && slots[2 * pair + 1] is { } b && a.Team.GroupLabel == b.Team.GroupLabel;
        var pairs = slots.Count / 2;
        for (var i = 0; i < pairs; i++)
        {
            if (!Clash(i)) continue;
            var lowIndex = slots[2 * i]!.SeedNo > slots[2 * i + 1]!.SeedNo ? 2 * i : 2 * i + 1;
            var tier = slots[lowIndex]!.Team.Position;
            for (var j = 0; j < pairs && Clash(i); j++)
            {
                if (j == i) continue;
                foreach (var k in new[] { 2 * j, 2 * j + 1 })
                {
                    if (slots[k] is not { } other || other.Team.Position != tier || slots[2 * j] is null || slots[2 * j + 1] is null) continue;
                    (slots[lowIndex], slots[k]) = (slots[k], slots[lowIndex]);
                    if (!Clash(i) && !Clash(j)) break;
                    (slots[lowIndex], slots[k]) = (slots[k], slots[lowIndex]);
                }
            }
        }
    }

    public static int NextPowerOfTwo(int n)
    {
        var p = 1;
        while (p < n) p *= 2;
        return p;
    }

    // Thứ tự vị trí chuẩn: hạt giống 1 gặp hạt giống cuối, giống BracketService của BRK-01.
    public static List<int> StandardSeedOrder(int size)
    {
        if (size <= 1) return new List<int> { 1 };
        var result = new List<int>();
        foreach (var p in StandardSeedOrder(size / 2))
        {
            result.Add(p);
            result.Add(size + 1 - p);
        }
        return result;
    }
}
