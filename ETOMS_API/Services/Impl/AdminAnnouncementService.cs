using ETOMS_API.DTOs.Admin;
using ETOMS_API.Models;
using ETOMS_API.Repositories;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;

namespace ETOMS_API.Services.Impl;

public class AdminAnnouncementService : IAdminAnnouncementService
{
    private const int BatchSize = 500;
    private static readonly TimeSpan DuplicateWindow = TimeSpan.FromSeconds(60);

    private readonly EtomsDbContext _db;
    private readonly IMailQueue _mail;
    private readonly IConfiguration _config;
    private readonly ILogger<AdminAnnouncementService> _log;

    public AdminAnnouncementService(EtomsDbContext db, IMailQueue mail, IConfiguration config,
        ILogger<AdminAnnouncementService> log)
    {
        _db = db;
        _mail = mail;
        _config = config;
        _log = log;
    }

    // ---------------------------------------------------------------- đếm người nhận

    public async Task<AudienceResult> CountAudienceAsync(string[]? roles, string? channel, CancellationToken ct = default)
    {
        var normalizedRoles = NormalizeRoles(roles);
        if (normalizedRoles == null) return new AudienceResult(AnnouncementError.InvalidRoles);
        var ch = NormalizeChannel(channel ?? AdminConstants.ChannelBoth);
        if (ch == null) return new AudienceResult(AnnouncementError.InvalidChannel);

        bool wantIn = ch != AdminConstants.ChannelEmail;
        bool wantMail = ch != AdminConstants.ChannelInApp;

        var q = _db.Set<Account>().AsNoTracking()
            .Where(a => a.Status == AdminConstants.AccountActive && normalizedRoles.Contains(a.Role));

        var accounts = await q.CountAsync(ct);
        var inApp = wantIn ? await q.CountAsync(a => a.NotifyInApp, ct) : 0;
        var email = wantMail ? await q.CountAsync(a => a.NotifyEmail, ct) : 0;
        var recipients = await q.CountAsync(a => (wantIn && a.NotifyInApp) || (wantMail && a.NotifyEmail), ct);

        return new AudienceResult(AnnouncementError.None, new AudienceCount(accounts, inApp, email, recipients));
    }

    // ---------------------------------------------------------------- gửi

    public async Task<AnnouncementSendResult> SendAsync(int actorId, SendAnnouncementRequest request, CancellationToken ct = default)
    {
        var title = (request.Title ?? "").Trim();
        if (title.Length < 3 || title.Length > 200) return new AnnouncementSendResult(AnnouncementError.InvalidTitle);

        var body = (request.Body ?? "").Trim();
        if (body.Length < 5 || body.Length > 1000) return new AnnouncementSendResult(AnnouncementError.InvalidBody);

        var roles = NormalizeRoles(request.Roles);
        if (roles == null) return new AnnouncementSendResult(AnnouncementError.InvalidRoles);

        var channel = NormalizeChannel(request.Channel ?? AdminConstants.ChannelInApp);
        if (channel == null) return new AnnouncementSendResult(AnnouncementError.InvalidChannel);

        if (!LinkRules.IsValidOptional(request.LinkUrl)) return new AnnouncementSendResult(AnnouncementError.InvalidLink);
        var link = LinkRules.Normalize(request.LinkUrl);

        // Chặn bấm đúp / gửi lặp cùng một tiêu đề trong 60 giây.
        var cutoff = DateTime.UtcNow - DuplicateWindow;
        if (await _db.Set<SystemAnnouncement>().AsNoTracking()
                .AnyAsync(a => a.SentByAccountId == actorId && a.Title == title && a.SentAt > cutoff, ct))
            return new AnnouncementSendResult(AnnouncementError.DuplicateRecent);

        bool wantIn = channel != AdminConstants.ChannelEmail;
        bool wantMail = channel != AdminConstants.ChannelInApp;
        var targetRoles = roles.Length == AdminConstants.Roles.Length ? AdminConstants.RolesAll : string.Join(",", roles);

        var emails = new List<(string To, string Name)>();
        int recipients = 0, inAppCount = 0;
        int announcementId;

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        int lastId = 0;
        while (true)
        {
            var batch = await _db.Set<Account>().AsNoTracking()
                .Where(a => a.AccountId > lastId && a.Status == AdminConstants.AccountActive && roles.Contains(a.Role))
                .OrderBy(a => a.AccountId)
                .Take(BatchSize)
                .Select(a => new { a.AccountId, a.Email, a.FullName, a.NotifyInApp, a.NotifyEmail })
                .ToListAsync(ct);
            if (batch.Count == 0) break;
            lastId = batch[^1].AccountId;

            var now = DateTime.UtcNow;
            var rows = new List<Notification>(batch.Count);
            foreach (var a in batch)
            {
                bool inApp = wantIn && a.NotifyInApp;     // tôn trọng cài đặt nhận thông báo của từng người
                bool mail = wantMail && a.NotifyEmail;
                if (!inApp && !mail) continue;

                rows.Add(new Notification
                {
                    AccountId = a.AccountId,
                    NotificationType = AdminConstants.NotificationSystem,
                    Title = title,
                    Body = body,
                    LinkUrl = link,
                    Channel = inApp && mail ? AdminConstants.ChannelBoth
                            : inApp ? AdminConstants.ChannelInApp
                            : AdminConstants.ChannelEmail,
                    IsRead = false,
                    SentAt = now
                });
                recipients++;
                if (inApp) inAppCount++;
                if (mail) emails.Add((a.Email, a.FullName));
            }

            if (rows.Count > 0)
            {
                _db.Set<Notification>().AddRange(rows);
                await _db.SaveChangesAsync(ct);
                _db.ChangeTracker.Clear(); // không giữ hàng nghìn entity trong bộ nhớ
            }
        }

        if (recipients == 0)
        {
            await tx.RollbackAsync(ct);
            return new AnnouncementSendResult(AnnouncementError.NoRecipients);
        }

        var announcement = new SystemAnnouncement
        {
            Title = title,
            Body = body,
            LinkUrl = link,
            TargetRoles = targetRoles,
            Channel = channel,
            InAppCount = inAppCount,
            EmailCount = emails.Count,
            SentByAccountId = actorId,
            SentAt = DateTime.UtcNow
        };
        _db.Set<SystemAnnouncement>().Add(announcement);
        await _db.SaveChangesAsync(ct);
        announcementId = announcement.SystemAnnouncementId;

        AdminAudit.Add(_db, actorId, AdminAuditActions.AnnounceSend, "SYSTEM_ANNOUNCEMENT", announcementId,
            null,
            new { roles = targetRoles, channel, recipients, inApp = inAppCount, emails = emails.Count, linkUrl = link },
            title);
        await _db.SaveChangesAsync(ct);

        await tx.CommitAsync(ct);

        // Mail chỉ xếp hàng sau khi commit; lỗi gửi do worker nền ghi log, không ảnh hưởng thông báo đã tạo.
        int queued = 0;
        if (emails.Count > 0)
        {
            var absoluteLink = ToAbsoluteLink(link);
            foreach (var (to, name) in emails)
            {
                if (_mail.TryEnqueue(new QueuedMail(to, "[ETOMS] " + title, BuildHtml(name, title, body, absoluteLink))))
                    queued++;
            }
            _log.LogInformation("Thông báo hệ thống \"{Title}\": {N} mail đã xếp hàng đợi", title, queued);
        }

        return new AnnouncementSendResult(AnnouncementError.None,
            new AnnouncementSent(announcementId, recipients, inAppCount, queued));
    }

    // ---------------------------------------------------------------- lịch sử

    public async Task<AnnouncementHistoryResponse> HistoryAsync(int page, int pageSize, CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        pageSize = Math.Clamp(pageSize, 1, 50);

        var q = _db.Set<SystemAnnouncement>().AsNoTracking();
        var total = await q.CountAsync(ct);

        var rows = await q.OrderByDescending(a => a.SystemAnnouncementId)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);

        var senderIds = rows.Select(r => r.SentByAccountId).Distinct().ToList();
        var names = await _db.Set<Account>().AsNoTracking()
            .Where(a => senderIds.Contains(a.AccountId))
            .Select(a => new { a.AccountId, a.FullName })
            .ToDictionaryAsync(a => a.AccountId, a => a.FullName, ct);

        var items = rows.Select(r => new AnnouncementHistoryItem(
            r.SystemAnnouncementId, r.SentAt, r.Title, r.Body, ParseRoles(r.TargetRoles), r.Channel,
            r.InAppCount, r.EmailCount, r.LinkUrl,
            names.TryGetValue(r.SentByAccountId, out var n) ? n : null)).ToList();

        return new AnnouncementHistoryResponse(items, total, page, pageSize);
    }

    // ---------------------------------------------------------------- nội bộ

    /// <summary>null = không hợp lệ. Rỗng hoặc có "ALL" = cả 3 vai trò.</summary>
    internal static string[]? NormalizeRoles(string[]? roles)
    {
        var list = (roles ?? Array.Empty<string>())
            .Select(r => (r ?? "").Trim().ToUpperInvariant())
            .Where(r => r.Length > 0)
            .Distinct()
            .ToList();

        if (list.Count == 0 || list.Contains(AdminConstants.RolesAll)) return AdminConstants.Roles.ToArray();
        if (list.Any(r => !AdminConstants.Roles.Contains(r))) return null;
        return AdminConstants.Roles.Where(list.Contains).ToArray();
    }

    internal static string? NormalizeChannel(string? channel)
    {
        var c = (channel ?? "").Trim().ToUpperInvariant();
        return AdminConstants.Channels.Contains(c) ? c : null;
    }

    internal static string[] ParseRoles(string? target)
    {
        if (string.IsNullOrWhiteSpace(target) || target.Trim().ToUpperInvariant() == AdminConstants.RolesAll)
            return AdminConstants.Roles.ToArray();
        return target.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private string? ToAbsoluteLink(string? link)
    {
        if (link == null) return null;
        if (!link.StartsWith('/')) return link;
        var baseUrl = (_config["AuthLinks:BaseUrl"] ?? "").Trim().TrimEnd('/');
        return baseUrl.Length == 0 ? null : baseUrl + link;
    }

    internal static string BuildHtml(string name, string title, string body, string? link)
    {
        var safeName = WebUtility.HtmlEncode(name);
        var safeTitle = WebUtility.HtmlEncode(title);
        var safeBody = WebUtility.HtmlEncode(body).Replace("\r\n", "\n").Replace("\n", "<br>");
        var linkHtml = link == null
            ? ""
            : $"<p><a href=\"{WebUtility.HtmlEncode(link)}\" style=\"color:#3B82F6\">Xem chi tiết</a></p>";

        return $"<div style=\"font-family:Arial,sans-serif;max-width:560px\">" +
               $"<p>Chào {safeName},</p><h3>{safeTitle}</h3><p>{safeBody}</p>{linkHtml}" +
               $"<p style=\"color:#888;font-size:12px\">Bạn nhận được email này vì đã bật nhận thông báo qua email trên ETOMS.</p></div>";
    }
}