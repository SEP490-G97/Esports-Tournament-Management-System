namespace ETOMS_API.DTOs.Team;

// Cho widget "Đội của tôi" trên nav — null khi tài khoản chưa ở trong đội nào.
public class MyTeamDto
{
    public int TeamId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Tag { get; set; } = string.Empty;
}
