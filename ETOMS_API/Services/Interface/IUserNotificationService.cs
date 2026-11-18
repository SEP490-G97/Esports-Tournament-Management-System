using ETOMS_API.DTOs;

namespace ETOMS_API.Services.Interface;

/// <summary>Hộp thư thông báo trong ứng dụng của người dùng đang đăng nhập (chỉ các thông báo kênh IN_APP / BOTH).</summary>
public interface IUserNotificationService
{
    Task<UserNotificationList> ListAsync(int accountId, int page, int pageSize, bool unreadOnly, CancellationToken ct = default);
    Task<int> UnreadCountAsync(int accountId, CancellationToken ct = default);
    /// <summary>false = không tìm thấy (hoặc không thuộc về người này). Đã đọc rồi vẫn trả true.</summary>
    Task<bool> MarkReadAsync(int accountId, int notificationId, CancellationToken ct = default);
    Task<int> MarkAllReadAsync(int accountId, CancellationToken ct = default);
}