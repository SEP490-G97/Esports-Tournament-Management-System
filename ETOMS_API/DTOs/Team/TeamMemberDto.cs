namespace ETOMS_API.DTOs.Team;

// Một thành viên (hoặc đơn chờ duyệt) trong roster (v2). Vai trò đội trưởng suy từ
// Team.CaptainAccountId, không phải từ Slot (Slot chỉ có STARTER / SUBSTITUTE).
public class TeamMemberDto
{
    public int AccountId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string Slot { get; set; } = string.Empty;    // STARTER / SUBSTITUTE
    public string Status { get; set; } = string.Empty;   // ACTIVE / PENDING / ...
    public string Direction { get; set; } = string.Empty; // JOIN_BY_CODE / APPLIED / INVITED
    public string? Message { get; set; }                  // lời nhắn khi nộp đơn (nếu có)
    public bool IsCaptain { get; set; }

    public string RoleLabel => IsCaptain
        ? "Đội trưởng"
        : Slot == "STARTER" ? "Chính thức" : "Dự bị";

    public string DirectionLabel => Direction switch
    {
        "APPLIED" => "Đã nộp đơn ứng tuyển",
        "INVITED" => "Được mời",
        "JOIN_BY_CODE" => "Vào bằng mã mời",
        _ => Direction
    };
}
