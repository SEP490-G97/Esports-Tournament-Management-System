using ETOMS_API.Models;

namespace ETOMS_API.Utils;

// Góp ý 23/11 mục 11: tài khoản mới phải nhận lời chào "Chào mừng đến ETOMS".
// Ghi thành MỘT thông báo trong web lúc tài khoản được kích hoạt (xác minh email xong / đăng nhập Google lần đầu /
// máy chủ không có SMTP nên kích hoạt ngay). Bộ gửi email chung (NotificationEmailInterceptor) tự gửi thêm bản email
// cho người bật nhận email khi máy chủ có SMTP — không viết đường gửi email thứ hai.
public static class WelcomeNotification
{
    public const string Title = "Chào mừng bạn đến với ETOMS";

    public static void AddTo(Account account)
        => account.Notifications.Add(new Notification
        {
            NotificationType = "SYSTEM",
            Title = Title,
            Body = $"Xin chào {account.FullName}, tài khoản của bạn đã sẵn sàng. Hãy cập nhật hồ sơ và tài khoản game, " +
                   "rồi tạo hoặc tham gia một đội để đăng ký giải đấu.",
            LinkUrl = "/profile/index.html",
            Channel = "IN_APP",
            IsRead = false,
            SentAt = DateTime.UtcNow
        });
}
