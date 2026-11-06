namespace ETOMS_API.DTOs.Bracket;

// BRK-02: 1 đội trong danh sách xếp hạt giống (đã được duyệt — APPROVED).
public class SeedingTeamDto
{
    public int TeamId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Tag { get; set; } = string.Empty;
    public int SeedRank { get; set; } // 1 = hạt giống cao nhất
}
