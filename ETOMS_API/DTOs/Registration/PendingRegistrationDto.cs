namespace ETOMS_API.DTOs.Registration;

// STUB TẠM thay cho REG-06/REG-07 (Danh sách chờ duyệt + Đối chiếu hồ sơ) của
// Cấn Việt Hùng — chỉ liệt kê đơn PENDING với nút Duyệt/Từ chối tối thiểu, để
// BRK-01 (sinh nhánh đấu) của Dũng có đội APPROVED mà thao tác.
public class PendingRegistrationDto
{
    public int RegistrationId { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public string TeamTag { get; set; } = string.Empty;
    public string CaptainName { get; set; } = string.Empty;
    public DateTime? SubmittedAt { get; set; }
}
