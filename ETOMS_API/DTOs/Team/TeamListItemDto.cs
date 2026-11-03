namespace ETOMS_API.DTOs.Team;

// Dữ liệu hiển thị 1 đội trong danh sách — chỉ field cần cho màn hình.
public class TeamListItemDto
{
    public int TeamId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Tag { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public string GameTitle { get; set; } = string.Empty;
    public string CaptainName { get; set; } = string.Empty;
    public int MemberCount { get; set; }
    public string Status { get; set; } = string.Empty;
}
