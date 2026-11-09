namespace ETOMS_API.DTOs.Registration;

// Giải đang mở đăng ký — cho dropdown chọn khi nộp đơn (stub REG-01) hoặc danh sách Organizer duyệt.
public class OpenTournamentDto
{
    public int TournamentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string GameTitle { get; set; } = string.Empty;
    public int MaxTeams { get; set; }
    public int ApprovedCount { get; set; }
    public DateTime RegCloseAt { get; set; }
    public bool RequirePaymentProof { get; set; }
}
