using System.ComponentModel.DataAnnotations;

namespace ETOMS_API.DTOs.Result;

// ===== RES-01 (UC-27): Báo cáo tỉ số mù =====

// Màn nộp báo cáo — không được lộ bất kỳ thông tin gì của báo cáo đối thủ (BR-RES-02).
public class ReportSubmissionPageDto
{
    public int MatchId { get; set; }
    public int TournamentId { get; set; }
    public string TournamentName { get; set; } = string.Empty;
    public int BestOf { get; set; }
    public string MyTeamName { get; set; } = string.Empty;
    public string OpponentTeamName { get; set; } = string.Empty;
    public string MatchStatus { get; set; } = string.Empty;
    public bool AlreadyReported { get; set; }
    public bool CanSubmit { get; set; }
    public string? CannotSubmitReason { get; set; }
}

// Nhập theo góc nhìn đội trưởng (đội tôi / đội đối thủ) — Service tự quy đổi sang TeamA/TeamB
// của Match để so khớp, đội trưởng không cần biết khái niệm "Team A/B" nội bộ của nhánh đấu.
public class SubmitReportRequest
{
    [Required(ErrorMessage = "Vui lòng nhập tỉ số đội bạn.")]
    [Range(0, 50, ErrorMessage = "Tỉ số không hợp lệ.")]
    public int MyScore { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tỉ số đội đối thủ.")]
    [Range(0, 50, ErrorMessage = "Tỉ số không hợp lệ.")]
    public int OpponentScore { get; set; }

    public string? PerGameScores { get; set; }
}

// ===== RES-04/RES-09 (UC-39): Minh chứng + lịch sử phán quyết công khai =====

public class EvidenceFileDto
{
    public string FileUrl { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty; // IMAGE | VIDEO | REPLAY | DOCUMENT
}

public class ReportEvidenceDto
{
    public string TeamName { get; set; } = string.Empty;
    public int ScoreA { get; set; }
    public int ScoreB { get; set; }
    public DateTime SubmittedAt { get; set; }
    public List<EvidenceFileDto> Files { get; set; } = new();
}

// BR-RES-02: chỉ hiện khi CẢ HAI báo cáo đã tồn tại (hết thời gian "mù") hoặc cửa sổ báo cáo đã đóng.
public class MatchEvidenceDto
{
    public int MatchId { get; set; }
    public string TeamAName { get; set; } = string.Empty;
    public string TeamBName { get; set; } = string.Empty;
    public string MatchStatus { get; set; } = string.Empty;
    public bool IsVisible { get; set; }
    public string? HiddenReason { get; set; }
    public List<ReportEvidenceDto> Reports { get; set; } = new();
    public bool CanFlag { get; set; }
}

public class AuditTrailEventDto
{
    public DateTime At { get; set; }
    public string Kind { get; set; } = string.Empty; // AUTO_VERIFY | FLAGGED | FORCE_UPDATE | APPEAL
    public string Title { get; set; } = string.Empty;
    public string? Detail { get; set; }
    public string? ActorName { get; set; }
}

public class MatchAuditTrailDto
{
    public int MatchId { get; set; }
    public string TeamAName { get; set; } = string.Empty;
    public string TeamBName { get; set; } = string.Empty;
    public List<AuditTrailEventDto> Events { get; set; } = new();

    // RES-08: khác NULL khi có phán quyết đã Resolved mà chưa ai khiếu nại — hiện form Appeal cho đội trưởng.
    public int? AppealableDisputeId { get; set; }
}

// ===== RES-05 (UC-30): Cắm cờ nghi vấn =====

public class FlagMatchRequest
{
    [Required(ErrorMessage = "Vui lòng chọn loại nghi vấn.")]
    public string Category { get; set; } = string.Empty; // WRONG_SCORE | CHEATING | INELIGIBLE_PLAYER | OTHER

    [Required(ErrorMessage = "Vui lòng nêu lý do cụ thể.")]
    [StringLength(1000, MinimumLength = 10, ErrorMessage = "Lý do phải từ 10 đến 1000 ký tự.")]
    public string Note { get; set; } = string.Empty;
}

// ===== RES-06 (UC-31): Hàng đợi tranh chấp =====

public class DisputeListItemDto
{
    public int DisputeId { get; set; }
    public int MatchId { get; set; }
    public int TournamentId { get; set; }
    public string TournamentName { get; set; } = string.Empty;
    public string TeamAName { get; set; } = string.Empty;
    public string TeamBName { get; set; } = string.Empty;
    public string OpenReason { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime OpenedAt { get; set; }
}

public class ChatLogItemDto
{
    public string SenderName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
}

public class DisputeDetailDto
{
    public int DisputeId { get; set; }
    public int MatchId { get; set; }
    public int TournamentId { get; set; }
    public string TeamAName { get; set; } = string.Empty;
    public int TeamAId { get; set; }
    public string TeamBName { get; set; } = string.Empty;
    public int TeamBId { get; set; }
    public string OpenReason { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? FlagCategory { get; set; }
    public string? FlagNote { get; set; }
    public string? FlaggedByName { get; set; }
    public List<ReportEvidenceDto> Reports { get; set; } = new();
    public List<ChatLogItemDto> ChatLog { get; set; } = new();
    public int? FinalScoreA { get; set; }
    public int? FinalScoreB { get; set; }
    public string? RulingReason { get; set; }
    public string? ResolvedByName { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public int AppealCount { get; set; }
    public string? AppealReason { get; set; }
    public string AppealStatus { get; set; } = "NONE";
    public string? AppealOutcomeNote { get; set; }
    public bool CanRule { get; set; }
}

// ===== RES-07 (UC-32): Phán quyết VAR (Force Update) =====

public class ForceUpdateRequest
{
    [Required(ErrorMessage = "Vui lòng nhập tỉ số đội A.")]
    [Range(0, 50, ErrorMessage = "Tỉ số không hợp lệ.")]
    public int ScoreA { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tỉ số đội B.")]
    [Range(0, 50, ErrorMessage = "Tỉ số không hợp lệ.")]
    public int ScoreB { get; set; }

    // true = không đội nào thắng (hủy kết quả, vd cả hai gian lận) — MSG-RES-004 áp dụng cho cả 2 trường hợp.
    public bool IsVoid { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập lý do phán quyết.")]
    [StringLength(1000, MinimumLength = 10, ErrorMessage = "Lý do phải từ 10 đến 1000 ký tự.")]
    public string Reason { get; set; } = string.Empty;
}

// ===== RES-08 (UC-33): Khiếu nại phán quyết =====

public class AppealRequest
{
    [Required(ErrorMessage = "Vui lòng nêu căn cứ khiếu nại.")]
    [StringLength(1000, MinimumLength = 10, ErrorMessage = "Căn cứ phải từ 10 đến 1000 ký tự.")]
    public string Reason { get; set; } = string.Empty;
}

public class AppealDecisionRequest
{
    public bool Upheld { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập kết luận khiếu nại.")]
    [StringLength(1000, MinimumLength = 10, ErrorMessage = "Kết luận phải từ 10 đến 1000 ký tự.")]
    public string OutcomeNote { get; set; } = string.Empty;

    // Chỉ cần khi Upheld = true (đổi tỉ số mới).
    public int? NewScoreA { get; set; }
    public int? NewScoreB { get; set; }
}
