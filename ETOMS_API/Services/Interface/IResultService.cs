using ETOMS_API.Utils;
using ETOMS_API.DTOs.Result;

namespace ETOMS_API.Services.Interface;

// Module RES (Kết quả & Khiếu nại / Result & VAR) — Nguyễn Tiến Dũng, Iteration 3.
// RES-01, 02, 04, 05, 06, 07, 08, 09 (RES-03 "Hết hạn báo cáo kết quả" là của Vũ Đức Hải).
// Nguồn: SEP490_G97_Report3 UC-27, UC-28, UC-30, UC-31, UC-32, UC-33, UC-39.
public interface IResultService
{
    // RES-01 (UC-27)
    Task<ReportSubmissionPageDto?> GetSubmissionPageAsync(int matchId, int captainAccountId);
    Task<Result> SubmitReportAsync(int matchId, int captainAccountId, SubmitReportRequest request, List<(string Url, string FileType, long? SizeBytes)> evidenceFiles);

    // RES-02 (UC-28) — không có màn hình riêng, được gọi tự động ngay khi báo cáo thứ hai được nộp
    // (SubmitReportAsync tự gọi), và lộ ra đây để có thể gọi lại thủ công (nối với RES-03 của Hải sau này).
    Task ReconcileAsync(int matchId);

    // RES-04 + RES-09 (UC-39)
    Task<MatchEvidenceDto?> GetEvidenceAsync(int matchId, int? viewerAccountId);
    Task<MatchAuditTrailDto?> GetAuditTrailAsync(int matchId);

    // RES-05 (UC-30)
    Task<Result> FlagMatchAsync(int matchId, int accountId, FlagMatchRequest request);

    // RES-06 (UC-31)
    Task<IReadOnlyList<DisputeListItemDto>> GetDisputeQueueAsync(int adjudicatorAccountId);
    Task<DisputeDetailDto?> GetDisputeDetailAsync(int disputeId, int viewerAccountId);
    // null = tranh chấp không tồn tại; false/true = có tồn tại, có/không phải Trọng tài gán hoặc BTC.
    Task<bool?> IsAdjudicatorAsync(int disputeId, int accountId);

    // RES-07 (UC-32)
    Task<Result> ForceUpdateAsync(int disputeId, int adjudicatorAccountId, ForceUpdateRequest request);

    // RES-08 (UC-33)
    Task<Result> AppealAsync(int disputeId, int captainAccountId, AppealRequest request);
    Task<Result> DecideAppealAsync(int disputeId, int adjudicatorAccountId, AppealDecisionRequest request);

    Task<bool> IsMatchCaptainAsync(int matchId, int accountId);
    Task<int?> GetCaptainTeamIdAsync(int matchId, int accountId);

    // Đẩy đội thắng lên vị trí vòng trên — dùng chung cho RES-02/07 và MAT-09/11/12 (walkover,
    // disqualify, forfeit cũng là một cách trận kết thúc có người thắng).
    Task AdvanceWinnerAsync(int matchId, int winnerTeamId);
}
