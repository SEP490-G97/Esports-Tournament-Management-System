using System.Net;
using System.Runtime.CompilerServices;
using ETOMS_API.Models;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;

namespace ETOMS_API.Repositories;

// Góp ý nhóm 18/11 mục 22: "Đã tích nhận thông báo qua email nhưng không có email".
// Trước đây cài đặt Account.NotifyEmail chỉ được đọc ở thông báo hệ thống và nhắc điểm danh; mọi thông báo khác
// (duyệt đơn, mời vào đội, kết quả trận, tranh chấp, thay người...) chỉ ghi vào chuông trong web.
// Bộ chặn này là MỘT chỗ chung: sau khi lưu thành công, mỗi dòng Notification mới có Channel = IN_APP mà người nhận
// bật nhận email (và tài khoản đang hoạt động) thì gửi thêm email qua hàng đợi IMailQueue (không chặn request).
// Không gửi trùng: nơi nào tự gửi email thì ghi Channel EMAIL/BOTH (nhắc điểm danh) hoặc bọc trong Suppress()
// (thông báo hệ thống — quản trị viên đã tự chọn kênh gửi).
public sealed class NotificationEmailInterceptor : SaveChangesInterceptor
{
    private static readonly AsyncLocal<int> SuppressDepth = new();
    private readonly ConditionalWeakTable<DbContext, List<Notification>> _pending = new();
    private readonly IMailQueue _queue;
    private readonly string _baseUrl;

    public NotificationEmailInterceptor(IMailQueue queue, IOptions<AuthLinkOptions> links)
    {
        _queue = queue;
        _baseUrl = (links.Value.BaseUrl ?? "").TrimEnd('/');
    }

    /// <summary>Tắt gửi email tự động trong khối using (nơi đã tự gửi email theo lựa chọn riêng).</summary>
    public static IDisposable Suppress()
    {
        SuppressDepth.Value++;
        return new Releaser();
    }

    private sealed class Releaser : IDisposable
    {
        private bool _done;
        public void Dispose() { if (!_done) { _done = true; SuppressDepth.Value--; } }
    }

    private void Collect(DbContext? ctx)
    {
        if (ctx is null || SuppressDepth.Value > 0) return;
        var added = ctx.ChangeTracker.Entries<Notification>()
            .Where(e => e.State == EntityState.Added && e.Entity.Channel == "IN_APP")
            .Select(e => e.Entity).ToList();
        if (added.Count > 0) _pending.AddOrUpdate(ctx, added);
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
        Send(eventData.Context);
        return result;
    }

    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        Send(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData) { if (eventData.Context is { } c) _pending.Remove(c); }

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } c) _pending.Remove(c);
        return Task.CompletedTask;
    }

    private void Send(DbContext? ctx)
    {
        if (ctx is null || !_pending.TryGetValue(ctx, out var list)) return;
        _pending.Remove(ctx);
        var ids = list.Select(n => n.AccountId).Distinct().ToList();
        // Truy vấn đồng bộ, ngắn (chỉ email + cài đặt của người nhận); lỗi đọc thì bỏ qua email, không làm hỏng thao tác đã lưu.
        List<(int Id, string Email, string Name)> to;
        try
        {
            to = ctx.Set<Account>().AsNoTracking()
                .Where(a => ids.Contains(a.AccountId) && a.NotifyEmail && a.Status == "ACTIVE")
                .Select(a => new { a.AccountId, a.Email, a.FullName }).AsEnumerable()
                .Select(a => (a.AccountId, a.Email, a.FullName)).ToList();
        }
        catch { return; }

        foreach (var n in list)
        {
            var r = to.FirstOrDefault(x => x.Id == n.AccountId);
            if (r.Email is null) continue;
            var link = string.IsNullOrEmpty(n.LinkUrl) ? null
                : n.LinkUrl.StartsWith('/') ? _baseUrl + n.LinkUrl : n.LinkUrl;
            var html = $@"<div style=""font-family:Arial,sans-serif;font-size:15px;line-height:1.6;color:#1f2430"">
                <p>Xin chào <b>{WebUtility.HtmlEncode(r.Name)}</b>,</p>
                <p><b>{WebUtility.HtmlEncode(n.Title)}</b></p>
                <p>{WebUtility.HtmlEncode(n.Body).Replace("\n", "<br/>")}</p>
                {(link is null ? "" : $@"<p><a href=""{WebUtility.HtmlEncode(link)}"">Mở trên ETOMS</a></p>")}
                <p style=""color:#6b7280;font-size:13px"">Bạn nhận email này vì đã bật ""Nhận thông báo qua email"" trong hồ sơ ETOMS. Tắt ở mục Giấy tờ &amp; riêng tư của hồ sơ cá nhân.</p></div>";
            _queue.TryEnqueue(new QueuedMail(r.Email, "[ETOMS] " + n.Title, html));
        }
    }
}
