using ETOMS_API.DTOs;
using ETOMS_API.Models;
using ETOMS_API.Repositories;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Services.Impl;

public class UserNotificationService : IUserNotificationService
{
    private readonly EtomsDbContext _db;

    public UserNotificationService(EtomsDbContext db) => _db = db;

    // Thông báo chỉ gửi email (Channel = EMAIL) không hiện trong chuông.
    private IQueryable<Notification> Inbox(int accountId) =>
        _db.Set<Notification>().Where(n => n.AccountId == accountId && n.Channel != AdminConstants.ChannelEmail);

    public async Task<UserNotificationList> ListAsync(int accountId, int page, int pageSize, bool unreadOnly, CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        pageSize = Math.Clamp(pageSize, 1, 50);

        var q = Inbox(accountId).AsNoTracking();
        var unread = await q.CountAsync(n => !n.IsRead, ct);
        if (unreadOnly) q = q.Where(n => !n.IsRead);
        var total = await q.CountAsync(ct);

        var rows = await q.OrderByDescending(n => n.SentAt).ThenByDescending(n => n.NotificationId)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);

        var items = rows.Select(n => new UserNotificationItem(
            n.NotificationId, n.NotificationType, n.Title, n.Body, n.LinkUrl, n.IsRead,
            DateTime.SpecifyKind(n.SentAt, DateTimeKind.Utc))).ToList();

        return new UserNotificationList(items, total, unread, page, pageSize);
    }

    public Task<int> UnreadCountAsync(int accountId, CancellationToken ct = default) =>
        Inbox(accountId).AsNoTracking().CountAsync(n => !n.IsRead, ct);

    public async Task<bool> MarkReadAsync(int accountId, int notificationId, CancellationToken ct = default)
    {
        var n = await Inbox(accountId).FirstOrDefaultAsync(x => x.NotificationId == notificationId, ct);
        if (n == null) return false;
        if (!n.IsRead)
        {
            n.IsRead = true;
            await _db.SaveChangesAsync(ct);
        }
        return true;
    }

    public async Task<int> MarkAllReadAsync(int accountId, CancellationToken ct = default)
    {
        var rows = await Inbox(accountId).Where(n => !n.IsRead).ToListAsync(ct);
        if (rows.Count == 0) return 0;
        foreach (var n in rows) n.IsRead = true;
        await _db.SaveChangesAsync(ct);
        return rows.Count;
    }
}