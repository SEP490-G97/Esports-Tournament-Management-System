namespace ETOMS_API.Models;

// Thông báo hệ thống quản trị viên gửi tới nhiều người (bảng có sẵn SystemAnnouncement): mỗi lần gửi một dòng,
// từng người nhận có một dòng Notification (trong web) và/hoặc email.
public partial class SystemAnnouncement
{
    public int SystemAnnouncementId { get; set; }
    public string Title { get; set; } = null!;
    public string Body { get; set; } = null!;
    public string? LinkUrl { get; set; }
    public string TargetRoles { get; set; } = null!;   // ALL | PLAYER | ORGANIZER
    public string Channel { get; set; } = null!;        // IN_APP | EMAIL | BOTH
    public int InAppCount { get; set; }
    public int EmailCount { get; set; }
    public int SentByAccountId { get; set; }
    public DateTime SentAt { get; set; }
    public virtual Account SentByAccount { get; set; } = null!;
}
