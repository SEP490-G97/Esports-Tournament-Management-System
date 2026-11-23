using System.Net;
using System.Runtime.CompilerServices;
using ETOMS_API.Models;
using ETOMS_API.Services.Impl;
using ETOMS_API.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;

namespace ETOMS_API.Repositories;

// Góp ý nhóm 18/11 mục 22: "Đã tích nhận thông báo qua email nhưng không có email".
// Trước đây cài đặt Account.NotifyEmail chỉ được đọc ở thông báo hệ thống và nhắc điểm danh; mọi thông báo khác
// (duyệt đơn, mời vào đội, kết quả trận, tranh chấp, thay người...) chỉ ghi vào chuông trong web.
// Bộ chặn này là MỘT chỗ chung: sau khi lưu thành công, mỗi dòng Notification mới có Channel = IN_APP mà người nhận
// bật nhận email (và tài khoản đang hoạt động) thì gửi thêm email qua hàng đợi NotificationMailQueue (không chặn request).
// Không gửi trùng: nơi nào tự gửi email thì ghi Channel EMAIL/BOTH (nhắc điểm danh) hoặc bọc trong Suppress()
// (thông báo hệ thống — quản trị viên đã tự chọn kênh gửi).
public sealed class NotificationEmailInterceptor : SaveChangesInterceptor
{
    private static readonly AsyncLocal<int> SuppressDepth = new();
    private readonly ConditionalWeakTable<DbContext, List<Notification>> _pending = new();
    private readonly NotificationMailQueue _queue;
    private readonly string _baseUrl;

    public NotificationEmailInterceptor(NotificationMailQueue queue, IOptions<AuthLinkOptions> links)
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

    // UC-40 A1: thông báo ràng buộc người nhận (đổi thể lệ, hủy giải) luôn được ghi, kể cả khi đã tắt thông báo trong web.
    private static readonly ConditionalWeakTable<Notification, object> BindingSet = new();
    private static readonly object BindingMarker = new();
    public static Notification Binding(Notification n) { BindingSet.AddOrUpdate(n, BindingMarker); return n; }

    private void Collect(DbContext? ctx)
    {
        if (ctx is null || SuppressDepth.Value > 0) return;
        var entries = ctx.ChangeTracker.Entries<Notification>()
            .Where(e => e.State == EntityState.Added && e.Entity.Channel == "IN_APP")
            .ToList();
        if (entries.Count == 0) return;

        // UC-40 bước 4: ghi in-app, email hoặc cả hai theo cài đặt của người nhận.
        Dictionary<int, (bool InApp, bool Email)> prefs;
        try
        {
            var ids = entries.Select(e => e.Entity.AccountId).Distinct().ToList();
            prefs = ctx.Set<Account>().AsNoTracking().Where(a => ids.Contains(a.AccountId))
                .Select(a => new { a.AccountId, a.NotifyInApp, a.NotifyEmail }).AsEnumerable()
                .ToDictionary(a => a.AccountId, a => (a.NotifyInApp, a.NotifyEmail));
        }
        catch { prefs = new(); }

        var added = new List<Notification>();
        foreach (var e in entries)
        {
            var n = e.Entity;
            var (inApp, email) = prefs.TryGetValue(n.AccountId, out var p) ? p : (true, true);
            if (!inApp && !BindingSet.TryGetValue(n, out _))
            {
                if (!email) { e.State = EntityState.Detached; continue; }   // tắt cả hai kênh: không ghi
                n.Channel = "EMAIL"; n.IsRead = true;                       // chỉ email: không hiện là thông báo mới trong web
            }
            added.Add(n);
        }
        if (added.Count > 0) _pending.AddOrUpdate(ctx, added);
    }

    // NUI-NOT-01: mẫu thông báo theo loại (ContentItem Kind=TEMPLATE, Status=PUBLISHED). Biến dùng được trong mẫu.
    public static readonly string[] TemplateVariables = { "{ten}", "{tieu_de}", "{noi_dung}", "{lien_ket}" };

    // UC-41 E1: mẫu nhắc tới biến mà sự kiện không có → không gửi (trả null) thay vì gửi thư hỏng.
    public static string? RenderTemplate(string template, string name, string title, string body, string? link)
    {
        var text = template.Replace("{ten}", name).Replace("{tieu_de}", title).Replace("{noi_dung}", body).Replace("{lien_ket}", link ?? "");
        if (System.Text.RegularExpressions.Regex.IsMatch(text, @"\{[a-z_]+\}")) return null;
        if (template.Contains("{lien_ket}") && link is null) return null;
        return text;
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

        Dictionary<string, (string Title, string Body)> templates;
        try
        {
            var types = list.Select(n => n.NotificationType).Distinct().ToList();
            templates = ctx.Set<ContentItem>().AsNoTracking()
                .Where(c => c.Kind == "TEMPLATE" && c.Status == "PUBLISHED" && c.NotificationType != null && types.Contains(c.NotificationType))
                .Select(c => new { c.NotificationType, c.Title, c.Body }).AsEnumerable()
                .ToDictionary(c => c.NotificationType!, c => (c.Title, c.Body));
        }
        catch { templates = new(); }

        const string footer = @"<p style=""color:#6b7280;font-size:13px"">Bạn nhận email này vì đã bật ""Nhận thông báo qua email"" trong hồ sơ ETOMS. Tắt ở mục Giấy tờ &amp; riêng tư của hồ sơ cá nhân.</p></div>";
        foreach (var n in list)
        {
            var r = to.FirstOrDefault(x => x.Id == n.AccountId);
            if (r.Email is null) continue;
            var link = string.IsNullOrEmpty(n.LinkUrl) ? null
                : n.LinkUrl.StartsWith('/') ? _baseUrl + n.LinkUrl : n.LinkUrl;
            string subject, html;
            if (templates.TryGetValue(n.NotificationType, out var tpl))
            {
                // NUI-NOT-01: email dựng từ mẫu của loại thông báo; UC-41 E1: mẫu hỏng thì không gửi, thông báo trong web vẫn còn.
                var s = RenderTemplate(tpl.Title, r.Name, n.Title, n.Body, link);
                var b = RenderTemplate(tpl.Body, r.Name, n.Title, n.Body, link);
                if (s is null || b is null) continue;
                subject = s;
                html = $@"<div style=""font-family:Arial,sans-serif;font-size:15px;line-height:1.6;color:#1f2430""><p>{WebUtility.HtmlEncode(b).Replace("\n", "<br/>")}</p>" + footer;
            }
            else
            {
                subject = "[ETOMS] " + n.Title;
                html = $@"<div style=""font-family:Arial,sans-serif;font-size:15px;line-height:1.6;color:#1f2430"">
                <p>Xin chào <b>{WebUtility.HtmlEncode(r.Name)}</b>,</p>
                <p><b>{WebUtility.HtmlEncode(n.Title)}</b></p>
                <p>{WebUtility.HtmlEncode(n.Body).Replace("\n", "<br/>")}</p>
                {(link is null ? "" : $@"<p><a href=""{WebUtility.HtmlEncode(link)}"">Mở trên ETOMS</a></p>")}" + footer;
            }
            _queue.TryEnqueue(new QueuedMail(r.Email, subject, html));
        }
    }
}
