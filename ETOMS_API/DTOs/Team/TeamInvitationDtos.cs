namespace ETOMS_API.DTOs.Team;

// TEAM-01: kết quả ô "Tìm tuyển thủ để mời" trên trang tạo đội. Không SĐT.
public class PlayerSearchItemDto
{
    public int AccountId { get; set; }
    public string FullName { get; set; } = string.Empty;
    // Góp ý 28/11: hiện đầy đủ email cạnh tên để phân biệt người trùng tên (người dùng chốt hiện nguyên email).
    public string? Email { get; set; }
    public string? AvatarUrl { get; set; }
    // Tên trong game của tựa game đang chọn (nếu tuyển thủ đã khai báo).
    public string? InGameName { get; set; }
    // Đã thuộc một đội của tựa game này -> không mời được.
    public bool AlreadyInTeam { get; set; }
}

// Lời mời vào đội đang chờ tuyển thủ trả lời (TeamMember Direction = INVITED, Status = PENDING).
public class MyTeamInvitationDto
{
    public int TeamMemberId { get; set; }
    public int TeamId { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public string TeamTag { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public string GameTitle { get; set; } = string.Empty;
    public string CaptainName { get; set; } = string.Empty;
    public string? Message { get; set; }
    public DateTime InvitedAt { get; set; }
}
