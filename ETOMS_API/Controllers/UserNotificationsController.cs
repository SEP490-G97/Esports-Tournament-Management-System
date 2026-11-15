using ETOMS_API.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics.Contracts;
using System.Security.Claims;

namespace ETOMS_API.Controllers;

/// <summary>Chuông thông báo của người dùng đăng nhập (mọi vai trò).</summary>
[ApiController]
[Authorize]
[Route("api/notifications")]
public class UserNotificationsController : ControllerBase
{
    private readonly IUserNotificationService _svc;

    public UserNotificationsController(IUserNotificationService svc) => _svc = svc;

    private bool TryGetAccountId(out int id)
    {
        var raw = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(raw, out id) && id > 0;
    }

    /// <summary>GET /api/notifications?page=1&amp;pageSize=10&amp;unreadOnly=false</summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 10,
        [FromQuery] bool unreadOnly = false, CancellationToken ct = default)
    {
        if (!TryGetAccountId(out var id)) return Unauthorized();
        Response.Headers.CacheControl = "no-store";
        return Ok(await _svc.ListAsync(id, page, pageSize, unreadOnly, ct));
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> UnreadCount(CancellationToken ct)
    {
        if (!TryGetAccountId(out var id)) return Unauthorized();
        Response.Headers.CacheControl = "no-store";
        return Ok(new DTOs.UnreadCount(await _svc.UnreadCountAsync(id, ct)));
    }

    [HttpPost("{notificationId:int}/read")]
    public async Task<IActionResult> MarkRead(int notificationId, CancellationToken ct)
    {
        if (!TryGetAccountId(out var id)) return Unauthorized();
        return await _svc.MarkReadAsync(id, notificationId, ct)
            ? NoContent()
            : NotFound(new { code = "NOTIFICATION_NOT_FOUND", message = "Không tìm thấy thông báo." });
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        if (!TryGetAccountId(out var id)) return Unauthorized();
        return Ok(new { marked = await _svc.MarkAllReadAsync(id, ct) });
    }
}