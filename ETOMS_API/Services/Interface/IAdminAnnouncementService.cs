using ETOMS_API.DTOs.Admin;

namespace ETOMS_API.Services.Interface;

public enum AnnouncementError
{
    None,
    InvalidTitle,
    InvalidBody,
    InvalidRoles,
    InvalidChannel,
    InvalidLink,
    NoRecipients,
    DuplicateRecent
}

public record AudienceResult(AnnouncementError Error, AudienceCount? Data = null);

public record AnnouncementSendResult(AnnouncementError Error, AnnouncementSent? Data = null);

public interface IAdminAnnouncementService
{
    /// <summary>Đếm số người sẽ nhận (chỉ tài khoản ACTIVE, đã tính cài đặt nhận thông báo) để admin xác nhận trước khi gửi.</summary>
    Task<AudienceResult> CountAudienceAsync(string[]? roles, string? channel, CancellationToken ct = default);

    /// <summary>
    /// Trong một giao dịch: tạo Notification (type SYSTEM) cho từng người nhận, ghi SystemAnnouncement (lịch sử + số lượng)
    /// và AuditLog (ANNOUNCE_SEND). Email chỉ được xếp hàng đợi SAU khi commit.
    /// </summary>
    Task<AnnouncementSendResult> SendAsync(int actorId, SendAnnouncementRequest request, CancellationToken ct = default);

    Task<AnnouncementHistoryResponse> HistoryAsync(int page, int pageSize, CancellationToken ct = default);
}