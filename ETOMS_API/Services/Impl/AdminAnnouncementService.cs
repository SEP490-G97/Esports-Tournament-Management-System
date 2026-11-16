
using ETOMS_API.DTOs;
using ETOMS_API.DTOs.Admin;
using ETOMS_API.Models;
using ETOMS_API.Repositories;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Mail;

namespace ETOMS_API.Services.Impl;

public class AdminAnnouncementService : IAdminAnnouncementService
{
    private const int BatchSize = 500;
    public const int MaxIncludeEmails = 200;
    public const int MaxExcludeEmails = 1000;
    public const string TargetCustom = "CUSTOM";
    private const string ExcludeMarker = ";EXCL=";
    private static readonly TimeSpan DuplicateWindow = TimeSpan.FromSeconds(60);

    private readonly EtomsDbContext _db;
    private readonly IMailQueue _mail;
    private readonly IConfiguration _config;
    private readonly ILogger<AdminAnnouncementService> _log;
    private readonly INotificationHub? _hub;

    public AdminAnnouncementService(EtomsDbContext db, IMailQueue mail, IConfiguration config,
        ILogger<AdminAnnouncementService> log, INotificationHub? hub = null)
    {
        _db = db;
        _mail = mail;
        _config = config;
        _log = log;
        _hub = hub;
    }

    /// <summary>Kế hoạch gửi đã chuẩn hóa. Custom = chỉ gửi cho Emails; ngược lại gửi theo Roles trừ Exclude.</summary>
    private sealed record Plan(
        AnnouncementError Error,
        string[] Roles,
        string Channel,
        bool WantIn,
        bool WantMail,
        bool Custom,
        List<string> Emails,
        List<string> Exclude);

    private static Plan Fail(AnnouncementError e) =>
        new(e, Array.Empty<string>(), "", false, false, false, new(), new());

    private static Plan MakePlan(string[]? roles, string? channel, string[]? emails, string[]? exclude, string defaultChannel)
    {
        var r = NormalizeRoles(roles);
        if (r == null) return Fail(AnnouncementError.InvalidRoles);
        var ch = NormalizeChannel(channel ?? defaultChannel);
        if (ch == null) return Fail(AnnouncementError.InvalidChannel);

        var inc = NormalizeEmails(emails, out var incBad);
        var exc = NormalizeEmails(exclude, out var excBad);
        if (incBad || excBad) return Fail(AnnouncementError.InvalidEmails);
        if (inc.Count > MaxIncludeEmails || exc.Count > MaxExcludeEmails) return Fail(AnnouncementError.TooManyEmails);

        bool custom = inc.Count > 0;
        return new Plan(AnnouncementError.None, r, ch, ch != AdminConstants.ChannelEmail, ch != AdminConstants.ChannelInApp,
            custom, inc, custom ? new List<string>() : exc);
    }

    private IQueryable<Account> Recipients(Plan p)
    {
        var q = _db.Set<Account>().AsNoTracking().Where(a => a.Status == AdminConstants.AccountActive);
        if (p.Custom)
        {
            var emails = p.Emails;
            return q.Where(a => emails.Contains(a.Email.ToLower()));
        }
        var roles = p.Roles;
        return q.Where(a => roles.Contains(a.Role));
    }

    // ---------------------------------------------------------------- đếm người nhận

    public Task<AudienceResult> CountAudienceAsync(string[]? roles, string? channel, CancellationToken ct = default) =>
        CountAudienceAsync(new SendAnnouncementRequest { Roles = roles, Channel = channel }, ct);

    public async Task<AudienceResult> CountAudienceAsync(SendAnnouncementRequest request, CancellationToken ct = default)
    {
        var p = MakePlan(request.Roles, request.Channel, request.Emails, request.ExcludeEmails, AdminConstants.ChannelBoth);
        if (p.Error != AnnouncementError.None) return new AudienceResult(p.Error);

        var q = Recipients(p);

        if (p.Custom)
        {
            var matched = await q.Select(a => a.Email.ToLower()).ToListAsync(ct);
            int accounts = matched.Count;
            int unknown = p.Emails.Count - matched.Distinct().Count();
            int external = p.WantMail ? unknown : 0;
            return new AudienceResult(AnnouncementError.None, new AudienceCount(
                accounts,
                p.WantIn ? accounts : 0,
                p.WantMail ? accounts + external : 0,
                accounts + external,
                ExternalEmails: external,
                Unreachable: p.WantMail ? 0 : unknown));
        }

        var exclude = p.Exclude;
        var kept = exclude.Count > 0 ? q.Where(a => !exclude.Contains(a.Email.ToLower())) : q;
        var wantIn = p.WantIn; var wantMail = p.WantMail;

        var total = await q.CountAsync(ct);
        var keptTotal = exclude.Count > 0 ? await kept.CountAsync(ct) : total;
        var inApp = wantIn ? await kept.CountAsync(a => a.NotifyInApp, ct) : 0;
        var email = wantMail ? await kept.CountAsync(a => a.NotifyEmail, ct) : 0;
        var recipients = await kept.CountAsync(a => (wantIn && a.NotifyInApp) || (wantMail && a.NotifyEmail), ct);

        return new AudienceResult(AnnouncementError.None,
            new AudienceCount(keptTotal, inApp, email, recipients, Excluded: total - keptTotal));
    }

    // ---------------------------------------------------------------- gửi

    public async Task<AnnouncementSendResult> SendAsync(int actorId, SendAnnouncementRequest request, CancellationToken ct = default)
    {
        var title = (request.Title ?? "").Trim();
        if (title.Length < 3 || title.Length > 200) return new AnnouncementSendResult(AnnouncementError.InvalidTitle);

        var body = (request.Body ?? "").Trim();
        if (body.Length < 5 || body.Length > 1000) return new AnnouncementSendResult(AnnouncementError.InvalidBody);

        var p = MakePlan(request.Roles, request.Channel, request.Emails, request.ExcludeEmails, AdminConstants.ChannelInApp);
        if (p.Error != AnnouncementError.None) return new AnnouncementSendResult(p.Error);

        if (!LinkRules.IsValidOptional(request.LinkUrl)) return new AnnouncementSendResult(AnnouncementError.InvalidLink);
        var link = LinkRules.Normalize(request.LinkUrl);

        // Chặn bấm đúp / gửi lặp cùng một tiêu đề trong 60 giây.
        var cutoff = DateTime.UtcNow - DuplicateWindow;
        if (await _db.Set<SystemAnnouncement>().AsNoTracking()
                .AnyAsync(a => a.SentByAccountId == actorId && a.Title == title && a.SentAt > cutoff, ct))
            return new AnnouncementSendResult(AnnouncementError.DuplicateRecent);

        var exclude = p.Exclude;
        int excludedCount = 0;
        if (exclude.Count > 0)
            excludedCount = await Recipients(p).CountAsync(a => exclude.Contains(a.Email.ToLower()), ct);

        var targetRoles = p.Custom
            ? TargetCustom
            : (p.Roles.Length == AdminConstants.Roles.Length ? AdminConstants.RolesAll : string.Join(",", p.Roles))
              + (excludedCount > 0 ? ExcludeMarker + excludedCount : "");

        var emails = new List<(string To, string Name)>();
        var pushes = new List<(int AccountId, UserNotificationItem Item)>();
        var matchedEmails = new HashSet<string>();
        int recipients = 0, inAppCount = 0, announcementId;

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        int lastId = 0;
        while (true)
        {
            var q = Recipients(p).Where(a => a.AccountId > lastId);
            if (exclude.Count > 0) q = q.Where(a => !exclude.Contains(a.Email.ToLower()));

            var batch = await q.OrderBy(a => a.AccountId).Take(BatchSize)
                .Select(a => new { a.AccountId, a.Email, a.FullName, a.NotifyInApp, a.NotifyEmail })
                .ToListAsync(ct);
            if (batch.Count == 0) break;
            lastId = batch[^1].AccountId;

            var now = DateTime.UtcNow;
            var rows = new List<Notification>(batch.Count);
            foreach (var a in batch)
            {
                // Gửi hàng loạt tôn trọng cài đặt từng người; gửi chỉ định (admin chủ động chọn) thì bỏ qua cài đặt.
                bool inApp = p.WantIn && (p.Custom || a.NotifyInApp);
                bool mail = p.WantMail && (p.Custom || a.NotifyEmail);
                if (p.Custom) matchedEmails.Add(a.Email.ToLowerInvariant());
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
                foreach (var n in rows.Where(n => n.Channel != AdminConstants.ChannelEmail))
                    pushes.Add((n.AccountId, new UserNotificationItem(
                        n.NotificationId, n.NotificationType, n.Title, n.Body, n.LinkUrl, false,
                        DateTime.SpecifyKind(n.SentAt, DateTimeKind.Utc))));
                _db.ChangeTracker.Clear(); // không giữ hàng nghìn entity trong bộ nhớ
            }
        }

        // Email chỉ định chưa có tài khoản: chỉ gửi được mail.
        int skipped = 0;
        if (p.Custom)
        {
            foreach (var e in p.Emails.Where(e => !matchedEmails.Contains(e)))
            {
                if (p.WantMail) { emails.Add((e, "bạn")); recipients++; }
                else skipped++;
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
            Channel = p.Channel,
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
            new
            {
                roles = targetRoles,
                channel = p.Channel,
                recipients,
                inApp = inAppCount,
                emails = emails.Count,
                linkUrl = link,
                mode = p.Custom ? "CUSTOM" : exclude.Count > 0 ? "EXCEPT" : "ROLES",
                onlyEmails = p.Custom ? p.Emails : null,
                excludeEmails = exclude.Count > 0 ? exclude.Take(200) : null,
                excluded = excludedCount,
                skipped
            },
            title);
        await _db.SaveChangesAsync(ct);

        await tx.CommitAsync(ct);

        // Sau khi commit: đẩy real-time cho người đang online, rồi xếp hàng mail.
        if (_hub != null)
        {
            try { foreach (var (accountId, item) in pushes) _hub.Publish(accountId, item); }
            catch (Exception ex) { _log.LogWarning(ex, "Đẩy thông báo real-time thất bại (bỏ qua)"); }
        }

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
            new AnnouncementSent(announcementId, recipients, inAppCount, queued, skipped));
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
            names.TryGetValue(r.SentByAccountId, out var n) ? n : null,
            ParseExcluded(r.TargetRoles), IsCustom(r.TargetRoles))).ToList();

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

    /// <summary>Chữ thường, bỏ trống/trùng. bad = có ít nhất một địa chỉ không hợp lệ.</summary>
    internal static List<string> NormalizeEmails(string[]? input, out bool bad)
    {
        bad = false;
        var result = new List<string>();
        var seen = new HashSet<string>();
        foreach (var raw in input ?? Array.Empty<string>())
        {
            var e = (raw ?? "").Trim().ToLowerInvariant();
            if (e.Length == 0) continue;
            if (!IsValidEmail(e)) { bad = true; continue; }
            if (seen.Add(e)) result.Add(e);
        }
        return result;
    }

    internal static bool IsValidEmail(string e)
    {
        if (e.Length > 254 || e.Any(char.IsWhiteSpace)) return false;
        return MailAddress.TryCreate(e, out var m) && m.Address == e && m.Host.Contains('.');
    }

    private static string BaseTarget(string? target)
    {
        var t = (target ?? "").Trim();
        var i = t.IndexOf(ExcludeMarker, StringComparison.Ordinal);
        return i >= 0 ? t[..i] : t;
    }

    internal static bool IsCustom(string? target) => BaseTarget(target).ToUpperInvariant() == TargetCustom;

    internal static int ParseExcluded(string? target)
    {
        var t = (target ?? "");
        var i = t.IndexOf(ExcludeMarker, StringComparison.Ordinal);
        return i >= 0 && int.TryParse(t[(i + ExcludeMarker.Length)..], out var n) ? n : 0;
    }

    internal static string[] ParseRoles(string? target)
    {
        var t = BaseTarget(target);
        if (string.IsNullOrWhiteSpace(t) || t.ToUpperInvariant() is AdminConstants.RolesAll or TargetCustom)
            return AdminConstants.Roles.ToArray();
        return t.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
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
               $"<p style=\"color:#888;font-size:12px\">Email này được gửi từ hệ thống ETOMS.</p></div>";
    }
}