namespace ETOMS_API.DTOs.Team;

// Trang hồ sơ đội công khai (v2): thông tin đội + danh sách thành viên.
public class TeamDetailDto
{
    public int TeamId { get; set; }
    public int CaptainAccountId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Tag { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public string GameTitle { get; set; } = string.Empty;
    public string CaptainName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public List<TeamMemberDto> Members { get; set; } = new();
    public int MemberCount => Members.Count;
    public int StarterCount => Members.Count(m => m.Slot == "STARTER");
    public int SubCount => Members.Count(m => m.Slot == "SUBSTITUTE");
}
