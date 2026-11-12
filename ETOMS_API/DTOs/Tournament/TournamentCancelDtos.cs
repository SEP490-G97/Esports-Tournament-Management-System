using System.ComponentModel.DataAnnotations;

namespace ETOMS_API.DTOs.Tournament;

// TOUR-11: hủy giải và danh sách đội cần hoàn lệ phí.
public class TournamentCancelBoardDto
{
    public int TournamentId { get; set; }
    public string TournamentName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string StatusLabel { get; set; } = string.Empty;
    public string StatusClass { get; set; } = string.Empty;
    public bool IsCancelled { get; set; }
    public bool CanCancel { get; set; }
    public string? BlockedReason { get; set; }
    public string? CancelReason { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancelledByName { get; set; }
    // Số đội trưởng sẽ được (hoặc đã được) báo.
    public int CaptainCount { get; set; }
    public decimal EntryFee { get; set; }
    public int ToRefundCount { get; set; }
    public int ToVerifyCount { get; set; }
    public List<RefundTeamDto> RefundTeams { get; set; } = new();
}

public class RefundTeamDto
{
    public int RegistrationId { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public string CaptainName { get; set; } = string.Empty;
    public string CaptainEmail { get; set; } = string.Empty;
    public string RegistrationStatusLabel { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public string PaymentLabel { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    // TO_REFUND: đã xác nhận nộp, cần hoàn. TO_VERIFY: đã nộp biên lai, chưa đối soát. REFUNDED: đã ghi nhận hoàn.
    public string Group { get; set; } = string.Empty;
}

public class CancelTournamentRequest
{
    [Required(ErrorMessage = "Vui lòng nhập lý do hủy giải.")]
    [MaxLength(500, ErrorMessage = "Lý do hủy tối đa 500 ký tự.")]
    public string Reason { get; set; } = string.Empty;
}
