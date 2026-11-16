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
        /// <summary>
        /// Có giá trị = CHỈ gửi cho các email này (bỏ qua Roles/ExcludeEmails, bỏ qua cài đặt nhận thông báo của người nhận).
        /// Email chưa có tài khoản vẫn nhận được nếu Channel là EMAIL hoặc BOTH. Tối đa 200.
        /// </summary>
        public string[]? Emails { get; set; }
        /// <summary>Gửi theo Roles nhưng LOẠI TRỪ các tài khoản có email này. Tối đa 1000.</summary>
        public string[]? ExcludeEmails { get; set; }
    }

    /// <summary>
    /// Accounts: tài khoản ACTIVE thuộc nhóm. InApp/Email: số người sẽ nhận qua kênh đó (đã tính cài đặt nhận thông báo).
    /// Recipients: số người nhận ít nhất một kênh.
    /// </summary>
    /// ExternalEmails: email chưa có tài khoản sẽ nhận mail (chế độ chỉ định). Excluded: số tài khoản bị loại trừ.
    /// Unreachable: email chưa có tài khoản nhưng kênh chỉ IN_APP nên không gửi được.
    public record AudienceCount(int Accounts, int InApp, int Email, int Recipients,
        int ExternalEmails = 0, int Excluded = 0, int Unreachable = 0);

    public record AnnouncementSent(int SystemAnnouncementId, int Recipients, int InAppCount, int EmailQueued, int Skipped = 0);

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
        string? SentBy,
        int Excluded = 0,
        bool Custom = false);

    public record AnnouncementHistoryResponse(List<AnnouncementHistoryItem> Items, int Total, int Page, int PageSize);
}
