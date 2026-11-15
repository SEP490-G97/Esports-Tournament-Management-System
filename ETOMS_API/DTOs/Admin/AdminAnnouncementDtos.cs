namespace ETOMS_API.DTOs.Admin
{
    public class SendAnnouncementRequest
    {
        public string? Title { get; set; }
        public string? Body { get; set; }
        /// <summary>PLAYER / ORGANIZER / ADMIN. Bỏ trống hoặc chứa "ALL" = toàn bộ người dùng.</summary>
        public string[]? Roles { get; set; }
        /// <summary>IN_APP / EMAIL / BOTH. Bỏ trống = IN_APP.</summary>
        public string? Channel { get; set; }
        /// <summary>Đường dẫn nội bộ (bắt đầu bằng "/") hoặc URL https.</summary>
        public string? LinkUrl { get; set; }
    }

    /// <summary>
    /// Accounts: tài khoản ACTIVE thuộc nhóm. InApp/Email: số người sẽ nhận qua kênh đó (đã tính cài đặt nhận thông báo).
    /// Recipients: số người nhận ít nhất một kênh.
    /// </summary>
    public record AudienceCount(int Accounts, int InApp, int Email, int Recipients);

    public record AnnouncementSent(int SystemAnnouncementId, int Recipients, int InAppCount, int EmailQueued);

    public record AnnouncementHistoryItem(
        int Id,
        DateTime SentAt,
        string Title,
        string Body,
        string[] Roles,
        string Channel,
        int InAppCount,
        int EmailCount,
        string? LinkUrl,
        string? SentBy);

    public record AnnouncementHistoryResponse(List<AnnouncementHistoryItem> Items, int Total, int Page, int PageSize);
}
