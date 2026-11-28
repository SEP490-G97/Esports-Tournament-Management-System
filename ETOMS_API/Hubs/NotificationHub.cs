using System.Runtime.CompilerServices;
using ETOMS_API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ETOMS_API.Hubs;

// Thông báo thời gian thực (góp ý 28/11: "gửi thông báo thì tài khoản khác phải thấy ngay, không cần tải lại trang").
// Trình duyệt đã đăng nhập mở một kết nối SignalR tới /hubs/notifications (layout.js); mỗi khi một dòng Notification
// mới được lưu, máy chủ đẩy ngay tới đúng tài khoản nhận để chuông cập nhật số chưa đọc và hiện thông báo nhỏ.
// Không kết nối được (mạng chặn WebSocket...) thì chuông vẫn tự hỏi lại định kỳ như trước.
[Authorize]
public sealed class NotificationHub : Hub
{
    public const string Path = "/hubs/notifications";
}

/// <summary>Định danh người dùng của SignalR = claim "sub" (mã tài khoản) trong JWT.</summary>
public sealed class SubClaimUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) => connection.User?.FindFirst("sub")?.Value;
}

/// <summary>
/// Sau khi lưu thành công, đẩy các thông báo mới (hiện trong web, chưa đọc) tới người nhận qua <see cref="NotificationHub"/>.
/// Một chỗ chung cho mọi nơi tạo Notification (duyệt đơn, mời vào đội, kết quả trận, thông báo hệ thống...).
/// Đăng ký SAU NotificationEmailInterceptor để thấy đúng cài đặt "Nhận thông báo trong web" của từng người.
/// </summary>
public sealed class NotificationPushInterceptor : SaveChangesInterceptor
{
    private readonly ConditionalWeakTable<DbContext, List<Notification>> _pending = new();
    private readonly IHubContext<NotificationHub> _hub;
    private readonly ILogger<NotificationPushInterceptor> _log;

    public NotificationPushInterceptor(IHubContext<NotificationHub> hub, ILogger<NotificationPushInterceptor> log)
    {
        _hub = hub;
        _log = log;
    }

    private void Collect(DbContext? ctx)
    {
        if (ctx is null) return;
        var added = ctx.ChangeTracker.Entries<Notification>().Where(e => e.State == EntityState.Added).Select(e => e.Entity).ToList();
        if (added.Count > 0) _pending.AddOrUpdate(ctx, added);
    }

    private void Push(DbContext? ctx)
    {
        if (ctx is null || !_pending.TryGetValue(ctx, out var list)) return;
        _pending.Remove(ctx);
        // Chỉ dòng đã lưu thật (có mã) và hiện trong web (IsRead = false); dòng chỉ gửi email thì không đẩy.
        foreach (var g in list.Where(n => n.NotificationId > 0 && !n.IsRead).GroupBy(n => n.AccountId))
        {
            var last = g.OrderByDescending(n => n.NotificationId).First();
            var payload = new { count = g.Count(), title = last.Title, body = last.Body, linkUrl = last.LinkUrl, type = last.NotificationType };
            // Không chờ: đẩy chậm hay lỗi cũng không làm chậm / hỏng thao tác vừa lưu.
            _ = _hub.Clients.User(g.Key.ToString()).SendAsync("notify", payload).ContinueWith(t =>
                _log.LogWarning(t.Exception, "Đẩy thông báo thời gian thực tới tài khoản {Id} thất bại", g.Key),
                TaskContinuationOptions.OnlyOnFaulted);
        }
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Collect(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Collect(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        Push(eventData.Context);
        return result;
    }

    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        Push(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData) { if (eventData.Context is { } c) _pending.Remove(c); }

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } c) _pending.Remove(c);
        return Task.CompletedTask;
    }
}
