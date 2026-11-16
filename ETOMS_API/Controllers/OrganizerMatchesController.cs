using ETOMS_API.DTOs;
using ETOMS_API.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ETOMS_API.Controllers;

/// <summary>Thao tác vận hành trận dành cho Ban tổ chức (ORGANIZER của giải đó) hoặc ADMIN.</summary>
[ApiController]
[Authorize(Roles = "ORGANIZER,ADMIN")]
[Route("api/organizer/matches/{matchId:int}")]
public class OrganizerMatchesController : ControllerBase
{
    private readonly IManualCheckInService _svc;

    public OrganizerMatchesController(IManualCheckInService svc) => _svc = svc;

    /// <summary>POST /api/organizer/matches/{id}/manual-check-in — { teamIds: [..], reason: "..." }</summary>
    [HttpPost("manual-check-in")]
    public async Task<IActionResult> ManualCheckIn(int matchId, [FromBody] ManualCheckInRequest request, CancellationToken ct)
    {
        var raw = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(raw, out var actor) || actor <= 0) return Unauthorized();
        var role = User.FindFirstValue("role") ?? User.FindFirstValue(ClaimTypes.Role);

        var r = await _svc.ApplyAsync(matchId, actor, role, request ?? new ManualCheckInRequest(null, null), ct);
        return r.Error == ManualCheckInError.None ? Ok(r.Data) : Map(r);
    }

    private IActionResult Err(int status, string code, string message) => StatusCode(status, new { code, message });

    private IActionResult Map(ManualCheckInResult r) => r.Error switch
    {
        ManualCheckInError.NotFound => Err(404, "MATCH_NOT_FOUND", "Không tìm thấy trận đấu."),
        ManualCheckInError.Forbidden => Err(403, "NOT_ORGANIZER", "Bạn không phải Ban tổ chức của giải này."),
        ManualCheckInError.InvalidReason => Err(400, "REASON_INVALID", "Lý do bắt buộc, từ 10 đến 500 ký tự."),
        ManualCheckInError.InvalidTeams => Err(400, "TEAMS_INVALID", "Chọn một hoặc hai đội thuộc trận này."),
        ManualCheckInError.TournamentClosed => Err(409, "TOURNAMENT_CLOSED", "Giải đã kết thúc hoặc bị hủy."),
        ManualCheckInError.InvalidStatus => Err(409, "MATCH_STATUS_INVALID",
            "Chỉ check-in thủ công được cho trận sẵn sàng (chưa check-in) hoặc trận đang bị cờ No-show."),
        ManualCheckInError.AlreadyCheckedIn => Err(409, "ALREADY_CHECKED_IN", "Đội đã check-in rồi."),
        ManualCheckInError.TeamNotAbsent => Err(409, "TEAM_NOT_NO_SHOW", "Chỉ đội bị gắn cờ No-show mới được gỡ cờ."),
        ManualCheckInError.RollbackBlocked => Err(409, "ROLLBACK_BLOCKED", r.Detail ?? "Không thể hoàn tác kết quả vì trận kế tiếp đã bắt đầu."),
        _ => Err(500, "UNKNOWN", "Lỗi không xác định.")
    };
}