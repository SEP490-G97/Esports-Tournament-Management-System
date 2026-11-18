using ETOMS_API.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics.Contracts;
using System.Security.Claims;
using System.Text.Json;

namespace ETOMS_API.Controllers;

/// <summary>Chuông thông báo của người dùng đăng nhập (mọi vai trò).</summary>
[ApiController]
[Authorize]
[Route("api/notifications")]
public class UserNotificationsController : ControllerBase
{
    /// <summary>Kết nối SSE tối đa 10 phút rồi đóng; trình duyệt tự nối lại (và lấy token mới).</summary>
    public static readonly TimeSpan MaxStreamLifetime = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(25);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IUserNotificationService _svc;
    private readonly INotificationHub _hub;

    public UserNotificationsController(IUserNotificationService svc, INotificationHub hub)
    {
        _svc = svc;
        _hub = hub;
    }

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

    /// <summary>
    /// GET /api/notifications/stream — Server-Sent Events. Sự kiện "notification" mang một UserNotificationItem (JSON camelCase);
    /// dòng ": ping" giữ kết nối. Client dùng fetch + Authorization (EventSource không gửi được header).
    /// </summary>
    [HttpGet("stream")]
    public async Task Stream(CancellationToken ct)
    {
        if (!TryGetAccountId(out var id)) { Response.StatusCode = StatusCodes.Status401Unauthorized; return; }

        Response.Headers.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache, no-store";
        Response.Headers["X-Accel-Buffering"] = "no"; // nginx: không đệm

        using var sub = _hub.Subscribe(id);
        using var life = CancellationTokenSource.CreateLinkedTokenSource(ct);
        life.CancelAfter(MaxStreamLifetime);

        try
        {
            await WriteAsync(": connected\n\n", life.Token);
            while (!life.IsCancellationRequested)
            {
                using var beat = CancellationTokenSource.CreateLinkedTokenSource(life.Token);
                beat.CancelAfter(HeartbeatInterval);
                try
                {
                    if (!await sub.Reader.WaitToReadAsync(beat.Token)) break; // hub đóng kết nối này
                    while (sub.Reader.TryRead(out var item))
                        await WriteAsync("event: notification\ndata: " + JsonSerializer.Serialize(item, Json) + "\n\n", life.Token);
                }
                catch (OperationCanceledException) when (!life.IsCancellationRequested)
                {
                    await WriteAsync(": ping\n\n", life.Token); // hết hạn nhịp tim, không phải bị hủy
                }
            }
        }
        catch (OperationCanceledException) { /* client ngắt hoặc hết 10 phút */ }
    }

    private async Task WriteAsync(string text, CancellationToken ct)
    {
        await Response.WriteAsync(text, ct);
        await Response.Body.FlushAsync(ct);
    }
}