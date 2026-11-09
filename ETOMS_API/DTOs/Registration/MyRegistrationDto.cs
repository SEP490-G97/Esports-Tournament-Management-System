namespace ETOMS_API.DTOs.Registration;

// REG-04: Theo dõi trạng thái đơn — 1 dòng trong "Đơn của đội tôi".
public class MyRegistrationDto
{
    public int RegistrationId { get; set; }
    public int TournamentId { get; set; }
    public string TournamentName { get; set; } = string.Empty;
    public string GameTitle { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;         // DRAFT/PENDING/APPROVED/REJECTED/WAITLISTED/WITHDRAWN/DISQUALIFIED
    public string PaymentStatus { get; set; } = string.Empty;  // UNPAID/PROOF_SUBMITTED/CONFIRMED/REFUNDED
    public string? ReviewNote { get; set; }
    public int? WaitlistPosition { get; set; }
    public bool RequirePaymentProof { get; set; }
    public DateTime? SubmittedAt { get; set; }

    public string StatusLabel => Status switch
    {
        "DRAFT" => "Nháp",
        "PENDING" => "Đang chờ duyệt",
        "APPROVED" => "Đã duyệt",
        "REJECTED" => "Bị từ chối",
        "WAITLISTED" => "Danh sách chờ",
        "WITHDRAWN" => "Đã rút đơn",
        "DISQUALIFIED" => "Bị loại",
        _ => Status
    };

    public string StatusClass => Status switch
    {
        "APPROVED" => "badge-active",
        "REJECTED" or "DISQUALIFIED" => "badge-danger",
        "WAITLISTED" => "badge-gold",
        _ => "badge-soft"
    };

    public string PaymentStatusLabel => PaymentStatus switch
    {
        "UNPAID" => "Chưa nộp",
        "PROOF_SUBMITTED" => "Chờ đối soát",
        "CONFIRMED" => "Đã xác nhận",
        "REFUNDED" => "Đã hoàn tiền",
        _ => PaymentStatus
    };

    // Chỉ được tải minh chứng khi giải yêu cầu và đơn chưa bị từ chối/rút.
    public bool CanUploadProof => RequirePaymentProof && Status != "REJECTED" && Status != "WITHDRAWN" && Status != "DISQUALIFIED";
}
