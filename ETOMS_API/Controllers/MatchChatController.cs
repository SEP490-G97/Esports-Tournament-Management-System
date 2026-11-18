
using ETOMS_API.DTOs;
using ETOMS_API.Hubs;
using ETOMS_API.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using System.Text;

namespace ETOMS_API.Controllers;

/// <summary>REST cho phòng chờ: tải lịch sử (khi mở trang / tải thêm) và xuất log làm bằng chứng. Gửi tin đi qua SignalR hub.</summary>
[ApiController]
[Authorize]
[Route("api/matches/{matchId:int}/chat")]
public class MatchChatController : ControllerBase
{
    private readonly IMatchChatService _svc;
    private readonly IHubContext<MatchChatHub> _hub;

    public MatchChatController(IMatchChatService svc, IHubContext<MatchChatHub> hub)
    {
        _svc = svc;
        _hub = hub;
    }

    private bool TryActor(out int id, out string? role)
    {
        var raw = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        role = User.FindFirstValue("role") ?? User.FindFirstValue(ClaimTypes.Role);
        return int.TryParse(raw, out id) && id > 0;
    }

    [HttpGet]
    public async Task<IActionResult> History(int matchId, [FromQuery] long? before, [FromQuery] int limit = 100, CancellationToken ct = default)
    {
        if (!TryActor(out var actor, out var role)) return Unauthorized();
        var r = await _svc.HistoryAsync(matchId, actor, role, before, limit, ct);
        if (r.Error != ChatError.None) return Map(r.Error);
        Response.Headers.CacheControl = "private, no-store";
        return Ok(r.Data);
    }

    /// <summary>Đường dự phòng khi trình duyệt không dùng được SignalR: vẫn lưu log và phát cho cả phòng.</summary>
    [HttpPost]
    public async Task<IActionResult> Send(int matchId, [FromBody] ChatSendBody body, CancellationToken ct)
    {
        if (!TryActor(out var actor, out var role)) return Unauthorized();
        var r = await _svc.SendAsync(matchId, actor, role, body?.Text, ct);
        if (r.Error != ChatError.None) return Map(r.Error);
        await _hub.Clients.Group(MatchChatHub.Group(matchId)).SendAsync("message", r.Data!, ct);
        return StatusCode(201, r.Data);
    }

    /// <summary>Tệp văn bản toàn bộ log (UTC) — BTC / trọng tài / admin.</summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export(int matchId, CancellationToken ct)
    {
        if (!TryActor(out var actor, out var role)) return Unauthorized();
        var r = await _svc.ExportAsync(matchId, actor, role, ct);
        if (r.Error != ChatError.None) return Map(r.Error);
        var sb = new StringBuilder();
        sb.AppendLine($"# Log chat phòng chờ trận #{matchId} (giờ UTC)");
        foreach (var m in r.Data!)
        {
            var who = m.SenderRole == "CAPTAIN" ? $"Đội trưởng {m.TeamName}" : m.SenderRole == "REFEREE" ? "Trọng tài" : "BTC";
            sb.AppendLine($"[{m.SentAt:yyyy-MM-dd HH:mm:ss}] #{m.Id} {m.SenderName} ({who}): {m.Text.Replace("\n", "\n    ")}");
        }
        return File(new UTF8Encoding(true).GetBytes(sb.ToString()), "text/plain; charset=utf-8", $"match-{matchId}-chat.txt");
    }

    private IActionResult Map(ChatError e) => e switch
    {
        ChatError.NotFound => NotFound(new { code = "NOT_FOUND", message = "Không tìm thấy trận đấu." }),
        ChatError.Forbidden => StatusCode(403, new { code = "FORBIDDEN", message = "Bạn không có quyền vào phòng chờ của trận này." }),
        ChatError.LobbyClosed => Conflict(new { code = "LOBBY_CLOSED", message = "Phòng chờ đã đóng." }),
        ChatError.RateLimited => StatusCode(429, new { code = "RATE_LIMITED", message = "Bạn gửi quá nhanh, vui lòng chậm lại." }),
        _ => BadRequest(new { code = MatchChatHub.Code(e), message = "Yêu cầu không hợp lệ." })
    };
}