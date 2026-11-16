using ETOMS_API.DTOs;
using System.Threading.Channels;

namespace ETOMS_API.Services.Interface
{
    /// <summary>Đăng ký nhận thông báo real-time của một tài khoản (mỗi tab trình duyệt một đăng ký). Dispose để hủy.</summary>
    public sealed class NotificationSubscription : IDisposable
    {
        private readonly Action _dispose;
        public ChannelReader<UserNotificationItem> Reader { get; }

        public NotificationSubscription(ChannelReader<UserNotificationItem> reader, Action dispose)
        {
            Reader = reader;
            _dispose = dispose;
        }

        public void Dispose() => _dispose();
    }

    /// <summary>
    /// Trung tâm đẩy thông báo tới các kết nối đang mở (SSE). Chỉ nằm trong bộ nhớ của MỘT tiến trình:
    /// chạy nhiều instance thì cần backplane (Redis...). Người không online vẫn nhận qua bảng Notification + polling.
    /// </summary>
    public interface INotificationHub
    {
        NotificationSubscription Subscribe(int accountId);
        void Publish(int accountId, UserNotificationItem item);
        int ConnectionCount { get; }
    }
}