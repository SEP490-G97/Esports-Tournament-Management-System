using System.Net;
using System.Security.Claims;
using ETOMS_API.Models;
using ETOMS_API.Repositories;
using ETOMS_API.Services.Impl;
using ETOMS_API.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ETOMS_API.Controllers;

// Quảng cáo & thông báo hệ thống cho quản trị viên (/admin/ads.html) — dùng 2 bảng có sẵn SystemBanner, SystemAnnouncement.
//  * Banner: ảnh + tiêu đề + liên kết, chạy trong khoảng thời gian, hiện ở đầu trang chủ (GET /api/home/banners).
//  * Thông báo hệ thống: gửi tới mọi tài khoản / chỉ Tuyển thủ / chỉ Ban tổ chức, qua web và/hoặc email (tôn trọng cài đặt
//    nhận thông báo của từng người), lưu lịch sử kèm số người nhận.
[ApiController]
[Authorize(Roles = AccountRoles.Admin)]
public sealed class AdminAdsController : ControllerBase
{
    private static readonly string[] BannerTypes = { "EVENT", "GIVEAWAY" };
    private static readonly string[] Channels = { "IN_APP", "EMAIL", "BOTH" };
    private static readonly string[] Targets = { "ALL", "PLAYER", "ORGANIZER" };

    private readonly EtomsDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly NotificationMailQueue _mail;
    private readonly string _baseUrl;

    public AdminAdsController(EtomsDbContext db, IWebHostEnvironment env, NotificationMailQueue mail, IOptions<AuthLinkOptions> links)
    {
        _db = db;
        _env = env;
        _mail = mail;
        _baseUrl = (links.Value.BaseUrl ?? "").TrimEnd('/');
    }

    private int ActorId => int.TryParse(User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    private ObjectResult Err(string message, int status = 400) => StatusCode(status, new { message });

    private static string State(SystemBanner b, DateTime now) =>
        !b.IsActive ? "INACTIVE" : now < b.StartAt ? "SCHEDULED" : now >= b.EndAt ? "EXPIRED" : "LIVE";

    // ---------------------------------------------------------------- Banner quảng cáo

    [HttpGet("api/admin/banners")]
    public async Task<IActionResult> ListBanners(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var rows = await _db.Set<SystemBanner>().AsNoTracking().OrderBy(b => b.DisplayOrder).ThenByDescending(b => b.SystemBannerId).ToListAsync(ct);
        Response.Headers.CacheControl = "no-store";
        return Ok(rows.Select(b => new
        {
            b.SystemBannerId, b.Title, b.Description, b.ImageUrl, b.LinkUrl, b.BannerType, b.IsActive, b.DisplayOrder,
            b.StartAt, b.EndAt, State = State(b, now)
        }));
    }

    public sealed class BannerForm
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? LinkUrl { get; set; }
        public string? BannerType { get; set; }
        public DateTime? StartAt { get; set; }
        public DateTime? EndAt { get; set; }
        public int DisplayOrder { get; set; } = 1;
        public bool IsActive { get; set; } = true;
        public IFormFile? ImageFile { get; set; }
        public string? ImageLink { get; set; }
    }

    [HttpPost("api/admin/banners")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public Task<IActionResult> CreateBanner([FromForm] BannerForm form, CancellationToken ct) => SaveBanner(null, form, ct);

    [HttpPut("api/admin/banners/{id:int}")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public Task<IActionResult> UpdateBanner(int id, [FromForm] BannerForm form, CancellationToken ct) => SaveBanner(id, form, ct);

    private async Task<IActionResult> SaveBanner(int? id, BannerForm f, CancellationToken ct)
    {
        var title = (f.Title ?? "").Trim();
        if (title.Length < 3 || title.Length > 200) return Err("Tiêu đề banner từ 3 đến 200 ký tự.");
        var type = (f.BannerType ?? "EVENT").Trim().ToUpperInvariant();
        if (!BannerTypes.Contains(type)) return Err("Loại banner không hợp lệ.");
        if (f.StartAt is not DateTime start || f.EndAt is not DateTime end) return Err("Chọn thời gian bắt đầu và kết thúc hiển thị.");
        start = DateTime.SpecifyKind(start.ToUniversalTime(), DateTimeKind.Utc);
        end = DateTime.SpecifyKind(end.ToUniversalTime(), DateTimeKind.Utc);
        if (start >= end) return Err("Thời gian kết thúc phải sau thời gian bắt đầu.");
        if (f.DisplayOrder < 1 || f.DisplayOrder > 99) return Err("Thứ tự hiển thị từ 1 đến 99.");
        // Góp ý nhóm 18/11: sự kiện là trang viết trong ETOMS (/events/index.html?id=), không dẫn người xem ra trang ngoài.
        // Liên kết (không bắt buộc) chỉ là nút "Xem thêm" tới một trang khác TRONG ETOMS (vd trang giải).
        var link = (f.LinkUrl ?? "").Trim();
        if (link.Length > 0 && !IsInternal(link))
            return Err("Liên kết phải là đường dẫn trong ETOMS (bắt đầu bằng /), ví dụ /bracket/view.html?id=12.");

        SystemBanner? b = null;
        if (id is int bid)
        {
            b = await _db.Set<SystemBanner>().FirstOrDefaultAsync(x => x.SystemBannerId == bid, ct);
            if (b is null) return Err("Không tìm thấy banner.", 404);
        }
        string? image = b?.ImageUrl;
        if (f.ImageFile is not null || !string.IsNullOrWhiteSpace(f.ImageLink))
        {
            // Dùng chung bộ lưu ảnh bìa giải: kiểm định dạng ảnh thật, tải ảnh từ liên kết về máy chủ (chặn SSRF).
            var (url, error) = await BannerImage.SaveAsync(f.ImageFile, f.ImageLink, _env, ct);
            if (error is not null) return Err(error);
            image = url;
        }
        if (string.IsNullOrEmpty(image)) return Err("Chọn ảnh banner (tải ảnh lên hoặc dán liên kết ảnh).");

        var now = DateTime.UtcNow;
        if (b is null)
        {
            b = new SystemBanner { CreatedByAccountId = ActorId, CreatedAt = now };
            _db.Set<SystemBanner>().Add(b);
        }
        b.Title = title;
        b.Description = string.IsNullOrWhiteSpace(f.Description) ? null : f.Description.Trim()[..Math.Min(500, f.Description.Trim().Length)];
        b.LinkUrl = link.Length == 0 ? null : link;
        b.BannerType = type;
        b.StartAt = start;
        b.EndAt = end;
        b.DisplayOrder = f.DisplayOrder;
        b.IsActive = f.IsActive;
        b.ImageUrl = image;
        b.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);
        // UC-43: thay đổi biểu ngữ có trong nhật ký (cần SystemBannerId nên ghi sau khi lưu).
        AuditTrail.Add(_db, User, id is null ? "BANNER_CREATE" : "BANNER_UPDATE", "BANNER", b.SystemBannerId, null,
            $"{b.Title}; Type={b.BannerType}; {b.StartAt:yyyy-MM-dd HH:mm}→{b.EndAt:yyyy-MM-dd HH:mm}; Order={b.DisplayOrder}; Active={b.IsActive}");
        await _db.SaveChangesAsync(ct);
        return Ok(new { message = id is null ? "Đã thêm banner." : "Đã lưu banner.", b.SystemBannerId });
    }

    [HttpPost("api/admin/banners/{id:int}/toggle")]
    public async Task<IActionResult> ToggleBanner(int id, CancellationToken ct)
    {
        var b = await _db.Set<SystemBanner>().FirstOrDefaultAsync(x => x.SystemBannerId == id, ct);
        if (b is null) return Err("Không tìm thấy banner.", 404);
        b.IsActive = !b.IsActive;
        b.UpdatedAt = DateTime.UtcNow;
        AuditTrail.Add(_db, User, "BANNER_UPDATE", "BANNER", b.SystemBannerId, $"Active={!b.IsActive}", $"Active={b.IsActive}");
        await _db.SaveChangesAsync(ct);
        return Ok(new { message = b.IsActive ? "Đã bật banner." : "Đã tắt banner." });
    }

    [HttpDelete("api/admin/banners/{id:int}")]
    public async Task<IActionResult> DeleteBanner(int id, CancellationToken ct)
    {
        var b = await _db.Set<SystemBanner>().FirstOrDefaultAsync(x => x.SystemBannerId == id, ct);
        if (b is null) return Err("Không tìm thấy banner.", 404);
        _db.Set<SystemBanner>().Remove(b);
        AuditTrail.Add(_db, User, "BANNER_DELETE", "BANNER", id, $"{b.Title}; Type={b.BannerType}", null);
        await _db.SaveChangesAsync(ct);
        return Ok(new { message = "Đã xóa banner." });
    }

    // Banner đang chạy cho trang chủ (công khai).
    [AllowAnonymous]
    [HttpGet("api/home/banners")]
    public async Task<IActionResult> LiveBanners(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var rows = await _db.Set<SystemBanner>().AsNoTracking()
            .Where(b => b.IsActive && b.StartAt <= now && b.EndAt > now)
            .OrderBy(b => b.DisplayOrder).ThenByDescending(b => b.SystemBannerId).Take(6)
            .Select(b => new { b.SystemBannerId, b.Title, b.Description, b.ImageUrl, b.LinkUrl, b.BannerType })
            .ToListAsync(ct);
        Response.Headers.CacheControl = "public, max-age=60";
        return Ok(rows);
    }

    // Trang chi tiết sự kiện (công khai): chỉ banner đang bật và đang trong thời gian hiển thị.
    [AllowAnonymous]
    [HttpGet("api/home/banners/{id:int}")]
    public async Task<IActionResult> LiveBanner(int id, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var b = await _db.Set<SystemBanner>().AsNoTracking()
            .Where(x => x.SystemBannerId == id && x.IsActive && x.StartAt <= now && x.EndAt > now)
            .Select(x => new { x.SystemBannerId, x.Title, x.Description, x.ImageUrl, x.LinkUrl, x.BannerType, x.StartAt, x.EndAt })
            .FirstOrDefaultAsync(ct);
        if (b is null) return Err("Sự kiện không tồn tại, đã kết thúc hoặc chưa bắt đầu.", 404);
        Response.Headers.CacheControl = "public, max-age=60";
        // Liên kết cũ trỏ ra ngoài (nhập trước khi có quy tắc này) thì không đưa ra trang.
        return Ok(new { b.SystemBannerId, b.Title, b.Description, b.ImageUrl, LinkUrl = b.LinkUrl is { } l && IsInternal(l) ? l : null, b.BannerType, b.StartAt, b.EndAt });
    }

    // Đường dẫn trong ETOMS: bắt đầu bằng một dấu / (không phải // — đó là liên kết sang trang khác).
    private static bool IsInternal(string link) => link.StartsWith('/') && !link.StartsWith("//") && !link.Contains('\\');

    // ---------------------------------------------------------------- Thông báo hệ thống

    [HttpGet("api/admin/announcements")]
    public async Task<IActionResult> ListAnnouncements(CancellationToken ct)
    {
        var rows = await _db.Set<SystemAnnouncement>().AsNoTracking().OrderByDescending(a => a.SentAt).Take(50)
            .Select(a => new { a.SystemAnnouncementId, a.Title, a.Body, a.LinkUrl, a.TargetRoles, a.Channel, a.InAppCount, a.EmailCount, a.SentAt, SentBy = a.SentByAccount.FullName })
            .ToListAsync(ct);
        Response.Headers.CacheControl = "no-store";
        return Ok(rows);
    }

    public sealed record AnnouncementRequest(string? Title, string? Body, string? LinkUrl, string? TargetRoles, string? Channel);

    [HttpPost("api/admin/announcements")]
    public async Task<IActionResult> SendAnnouncement([FromBody] AnnouncementRequest r, CancellationToken ct)
    {
        var title = (r.Title ?? "").Trim();
        var body = (r.Body ?? "").Trim();
        var target = (r.TargetRoles ?? "ALL").Trim().ToUpperInvariant();
        var channel = (r.Channel ?? "IN_APP").Trim().ToUpperInvariant();
        var link = (r.LinkUrl ?? "").Trim();
        if (title.Length < 3 || title.Length > 200) return Err("Tiêu đề từ 3 đến 200 ký tự.");
        if (body.Length < 5 || body.Length > 1000) return Err("Nội dung từ 5 đến 1000 ký tự.");
        if (!Targets.Contains(target)) return Err("Đối tượng nhận không hợp lệ.");
        if (!Channels.Contains(channel)) return Err("Kênh gửi không hợp lệ.");
        if (link.Length > 0 && !IsInternal(link)) return Err("Liên kết phải là đường dẫn trong ETOMS (bắt đầu bằng /).");

        var q = _db.Accounts.AsNoTracking().Where(a => a.Status == "ACTIVE" && a.Role != "ADMIN");
        if (target != "ALL") q = q.Where(a => a.Role == target);
        var people = await q.Select(a => new { a.AccountId, a.Email, a.FullName, a.NotifyInApp, a.NotifyEmail }).ToListAsync(ct);

        bool wantIn = channel is "IN_APP" or "BOTH", wantMail = channel is "EMAIL" or "BOTH";
        var now = DateTime.UtcNow;
        int inApp = 0, email = 0;
        var mails = new List<QueuedMail>();
        foreach (var p in people)
        {
            bool i = wantIn && p.NotifyInApp, m = wantMail && p.NotifyEmail;   // tôn trọng cài đặt của từng người
            if (!i && !m) continue;
            _db.Notifications.Add(new Notification
            {
                AccountId = p.AccountId, NotificationType = "SYSTEM", Title = title, Body = body,
                LinkUrl = link.Length == 0 ? null : link, Channel = i && m ? "BOTH" : i ? "IN_APP" : "EMAIL", IsRead = !i, SentAt = now
            });
            if (i) inApp++;
            if (m)
            {
                email++;
                var url = link.Length == 0 ? null : _baseUrl + link;
                mails.Add(new QueuedMail(p.Email, "[ETOMS] " + title,
                    $@"<div style=""font-family:Arial,sans-serif;font-size:15px;line-height:1.6;color:#1f2430""><p>Xin chào <b>{WebUtility.HtmlEncode(p.FullName)}</b>,</p>
                    <p><b>{WebUtility.HtmlEncode(title)}</b></p><p>{WebUtility.HtmlEncode(body).Replace("\n", "<br/>")}</p>
                    {(url is null ? "" : $@"<p><a href=""{WebUtility.HtmlEncode(url)}"">Mở trên ETOMS</a></p>")}<p>Đội ngũ ETOMS</p></div>"));
            }
        }
        _db.Set<SystemAnnouncement>().Add(new SystemAnnouncement
        {
            Title = title, Body = body, LinkUrl = link.Length == 0 ? null : link, TargetRoles = target, Channel = channel,
            InAppCount = inApp, EmailCount = email, SentByAccountId = ActorId, SentAt = now
        });
        AuditTrail.Add(_db, User, "ANNOUNCE_SEND", "ANNOUNCEMENT", null, null,
            $"{title}; Target={target}; Channel={channel}; InApp={inApp}; Email={email}");
        // Email đã tự xếp hàng ở trên theo đúng lựa chọn kênh — tắt bộ gửi email tự động để không gửi trùng.
        using (NotificationEmailInterceptor.Suppress()) await _db.SaveChangesAsync(ct);
        foreach (var m in mails) _mail.TryEnqueue(m);
        return Ok(new { message = $"Đã gửi: {inApp} người nhận trong web, {email} email.", inApp, email });
    }

    // ---------------------------------------------------------------- Mẫu nội dung (System Communications Page, tab "Mẫu nội dung")
    // Bảng có sẵn ContentItem (Kind = TEMPLATE), mỗi loại thông báo đúng một mẫu (UX_ContentItem_Template).
    // NUI-NOT-01: mẫu "Đang dùng" của một loại thì email của loại đó dựng từ mẫu (NotificationEmailInterceptor.RenderTemplate);
    // thông báo hệ thống có thể bắt đầu soạn từ một mẫu.
    public static readonly Dictionary<string, string> NotificationTypes = new()
    {
        ["REGISTRATION"] = "Đăng ký giải", ["CHECKIN_REMINDER"] = "Nhắc điểm danh", ["MATCH_RESULT"] = "Kết quả trận",
        ["DISPUTE"] = "Tranh chấp", ["PENALTY"] = "Kỷ luật", ["NEWS"] = "Tin tức và thể lệ", ["SYSTEM"] = "Hệ thống"
    };

    [HttpGet("api/admin/notification-templates")]
    public async Task<IActionResult> ListTemplates(CancellationToken ct)
    {
        var rows = await _db.ContentItems.AsNoTracking().Where(c => c.Kind == "TEMPLATE")
            .Select(c => new { c.ContentItemId, c.NotificationType, c.Title, c.Body, c.Status, c.UpdatedAt }).ToListAsync(ct);
        Response.Headers.CacheControl = "no-store";
        return Ok(new
        {
            Variables = NotificationEmailInterceptor.TemplateVariables,
            Templates = NotificationTypes.Select(t =>
            {
                var r = rows.FirstOrDefault(x => x.NotificationType == t.Key);
                return new { Type = t.Key, Label = t.Value, r?.ContentItemId, r?.Title, r?.Body, Status = r?.Status, r?.UpdatedAt };
            })
        });
    }

    public sealed record TemplateRequest(string? Title, string? Body, string? Status);

    [HttpPut("api/admin/notification-templates/{type}")]
    public async Task<IActionResult> SaveTemplate(string type, [FromBody] TemplateRequest r, CancellationToken ct)
    {
        type = (type ?? "").Trim().ToUpperInvariant();
        if (!NotificationTypes.ContainsKey(type)) return Err("Loại thông báo không hợp lệ.");
        var title = (r.Title ?? "").Trim();
        var body = (r.Body ?? "").Trim();
        var status = (r.Status ?? "PUBLISHED").Trim().ToUpperInvariant();
        if (title.Length < 3 || title.Length > 200) return Err("Tiêu đề mẫu từ 3 đến 200 ký tự.");
        if (body.Length < 5 || body.Length > 5000) return Err("Nội dung mẫu từ 5 đến 5000 ký tự.");
        if (status is not ("PUBLISHED" or "HIDDEN")) return Err("Trạng thái mẫu không hợp lệ.");
        // UC-41 E1 chặn từ lúc lưu: chỉ được dùng các biến mà mọi thông báo đều có.
        var unknown = System.Text.RegularExpressions.Regex.Matches(title + " " + body, @"\{[^}]*\}")
            .Select(m => m.Value).Where(v => !NotificationEmailInterceptor.TemplateVariables.Contains(v)).Distinct().ToList();
        if (unknown.Count > 0) return Err($"Mẫu dùng biến không có: {string.Join(", ", unknown)}. Chỉ dùng {string.Join(", ", NotificationEmailInterceptor.TemplateVariables)}.");

        var now = DateTime.UtcNow;
        var item = await _db.ContentItems.FirstOrDefaultAsync(c => c.Kind == "TEMPLATE" && c.NotificationType == type, ct);
        if (item is null)
        {
            item = new ContentItem { Kind = "TEMPLATE", NotificationType = type, AuthorAccountId = ActorId, IsPinned = false };
            _db.ContentItems.Add(item);
        }
        item.Title = title;
        item.Body = body;
        item.Status = status;
        if (status == "PUBLISHED") item.PublishedAt ??= now;
        item.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);
        return Ok(new { message = status == "PUBLISHED" ? $"Đã lưu mẫu \"{NotificationTypes[type]}\" và đang dùng cho email loại này." : $"Đã lưu mẫu \"{NotificationTypes[type]}\" (đang tắt).", item.ContentItemId });
    }
}