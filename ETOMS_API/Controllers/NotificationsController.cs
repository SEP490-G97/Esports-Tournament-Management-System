using ETOMS_API.Repositories;
using ETOMS_API.Services.Interface;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Controllers;

// Thông báo trong web của người đang đăng nhập (bảng Notification) — chuông ở đầu trang (layout.js).
// Mọi chức năng đều đã ghi Notification (duyệt đơn, mời đội, kết quả trận...) nhưng trước đây không có chỗ xem.
[ApiController]
[Route("api/notifications")]
public sealed class NotificationsController : ControllerBase
{
    private readonly EtomsDbContext _db;
    private readonly ICurrentUser _me;

    public NotificationsController(EtomsDbContext db, ICurrentUser me)
    {
        _db = db;
        _me = me;
    }

    // Kênh EMAIL là thông báo chỉ gửi email (không hiện trong web).
    private IQueryable<Models.Notification> Mine(int id) =>
        _db.Notifications.Where(n => n.AccountId == id && n.Channel != "EMAIL");

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken ct = default)
    {
        if (_me.UserId is not int id) return Unauthorized();
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 30);
        var q = Mine(id).AsNoTracking();
        var total = await q.CountAsync(ct);
        var unread = await q.CountAsync(n => !n.IsRead, ct);
        var items = await q.OrderByDescending(n => n.SentAt).ThenByDescending(n => n.NotificationId)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(n => new { n.NotificationId, n.NotificationType, n.Title, n.Body, n.LinkUrl, n.IsRead, n.SentAt })
            .ToListAsync(ct);
        Response.Headers.CacheControl = "no-store";
        return Ok(new { total, unread, page, pageSize, items });
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> UnreadCount(CancellationToken ct)
    {
        if (_me.UserId is not int id) return Unauthorized();
        Response.Headers.CacheControl = "no-store";
        return Ok(new { unread = await Mine(id).CountAsync(n => !n.IsRead, ct) });
    }

    [HttpPost("{notificationId:int}/read")]
    public async Task<IActionResult> MarkRead(int notificationId, CancellationToken ct)
    {
        if (_me.UserId is not int id) return Unauthorized();
        // Chỉ đúng một thông báo, và chỉ của chính người đang đăng nhập.
        var n = await Mine(id).Where(x => x.NotificationId == notificationId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsRead, true), ct);
        return n == 0 ? NotFound(new { message = "Không tìm thấy thông báo." }) : Ok(new { message = "Đã đánh dấu đã đọc." });
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        if (_me.UserId is not int id) return Unauthorized();
        var n = await Mine(id).Where(x => !x.IsRead).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsRead, true), ct);
        return Ok(new { message = n > 0 ? $"Đã đánh dấu {n} thông báo là đã đọc." : "Không có thông báo chưa đọc." });
    }
}
