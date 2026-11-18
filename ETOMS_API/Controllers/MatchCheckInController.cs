using System.Security.Claims;
using ETOMS_API.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ETOMS_API.Controllers;

/// <summary>Check-in điểm danh trước trận — dành cho đội trưởng của một trong hai đội.</summary>
[ApiController]
[Authorize]
[Route("api/matches/{matchId:int}/check-in")]
public class MatchCheckInController : ControllerBase
{
    private readonly ICheckInService _svc;

    public MatchCheckInController(ICheckInService svc) => _svc = svc;

    [HttpGet]
    public async Task<IActionResult> State(int matchId, CancellationToken ct)
    {
        if (!TryGetActorId(out var actor)) return Unauthorized();
        var r = await _svc.GetStateAsync(matchId, actor, ct);
        if (r.Error != CheckInError.None) return Map(r.Error);
        Response.Headers.CacheControl = "private, no-store";
        return Ok(r.Data);
    }

    [HttpPost]
    public async Task<IActionResult> CheckIn(int matchId, CancellationToken ct)
    {
        if (!TryGetActorId(out var actor)) return Unauthorized();
        var r = await _svc.CheckInAsync(matchId, actor, ct);
        return r.Error == CheckInError.None ? Ok(r.Data) : Map(r.Error);
    }

    private bool TryGetActorId(out int id)
    {
        var raw = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(raw, out id) && id > 0;
    }

    private IActionResult Err(int status, string code, string message) => StatusCode(status, new { code, message });

    private IActionResult Map(CheckInError e) => e switch
    {
        CheckInError.NotFound => Err(404, "MATCH_NOT_FOUND", "Không tìm thấy trận đấu."),
        CheckInError.Forbidden => Err(403, "NOT_CAPTAIN", "Chỉ đội trưởng của một trong hai đội mới được check-in."),
        CheckInError.NotReady => Err(409, "MATCH_NOT_READY", "Trận chưa sẵn sàng để check-in (chưa đủ hai đội hoặc không ở trạng thái sẵn sàng)."),
        CheckInError.NotScheduled => Err(409, "NOT_SCHEDULED", "Trận chưa có giờ thi đấu."),
        CheckInError.NotYetOpen => Err(409, "CHECKIN_NOT_OPEN", "Chưa đến giờ mở check-in (mở trước giờ thi đấu 30 phút)."),
        CheckInError.Closed => Err(409, "CHECKIN_CLOSED", "Đã hết thời gian check-in."),
        CheckInError.AlreadyCheckedIn => Err(409, "ALREADY_CHECKED_IN", "Đội của bạn đã check-in rồi."),
        _ => Err(500, "UNKNOWN", "Lỗi không xác định.")
    };
}