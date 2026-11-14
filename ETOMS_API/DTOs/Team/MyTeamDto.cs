namespace ETOMS_API.DTOs.Team;

// Cho widget "Đội của tôi" trên nav — null khi tài khoản chưa ở trong đội nào.
public class MyTeamDto
{
    public int TeamId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Tag { get; set; } = string.Empty;
}

// Một đội trong danh sách "Các đội của tôi" (trang /team/mine.html và ô chọn đội khi đăng tin LFP).
// Một tài khoản có thể ở nhiều đội, mỗi tựa game tối đa một đội.
public class MyTeamItemDto
{
    public int TeamId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Tag { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public int GameId { get; set; }
    public string GameTitle { get; set; } = string.Empty;
    public bool IsCaptain { get; set; }
    public string Slot { get; set; } = string.Empty;
    public int MemberCount { get; set; }
    public DateTime? JoinedAt { get; set; }
}
