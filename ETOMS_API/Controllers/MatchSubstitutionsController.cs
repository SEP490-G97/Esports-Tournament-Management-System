using ETOMS_API.Contracts;
using ETOMS_API.DTOs;
using ETOMS_API.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ETOMS_API.Controllers;

/// <summary>Thay người trong trận: đội trưởng gửi yêu cầu (MAT-05); trọng tài của trận — hoặc BTC nếu trận không có trọng tài — duyệt (MAT-06).</summary>
[ApiController]
[Authorize]
[Route("api/matches/{matchId:int}/substitutions")]
public class MatchSubstitutionsController : ControllerBase
{
    private readonly ISubstitutionService _svc;

    public MatchSubstitutionsController(ISubstitutionService svc) => _svc = svc;

    private bool TryActor(out int id, out string? role)
    {
        var raw = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        role = User.FindFirstValue("role") ?? User.FindFirstValue(ClaimTypes.Role);
        return int.TryParse(raw, out id) && id > 0;
    }

    [HttpGet]
    public async Task<IActionResult> Board(int matchId, CancellationToken ct)
    {
        if (!TryActor(out var actor, out var role)) return Unauthorized();
        var r = await _svc.GetBoardAsync(matchId, actor, role, ct);
        if (r.Error != SubstitutionError.None) return Map(r.Error);
        Response.Headers.CacheControl = "private, no-store";
        return Ok(r.Data);
    }

    [HttpPost]
    public async Task<IActionResult> Request(int matchId, [FromBody] SubstitutionRequestBody body, CancellationToken ct)
    {
        if (!TryActor(out var actor, out _)) return Unauthorized();
        var r = await _svc.RequestAsync(matchId, actor, body, ct);
        return r.Error == SubstitutionError.None ? StatusCode(201, r.Data) : Map(r.Error);
    }

    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int matchId, int id, CancellationToken ct)
    {
        if (!TryActor(out var actor, out _)) return Unauthorized();
        var r = await _svc.CancelAsync(matchId, id, actor, ct);
        return r.Error == SubstitutionError.None ? Ok(r.Data) : Map(r.Error);
    }

    [HttpPost("{id:int}/decision")]
    public async Task<IActionResult> Decide(int matchId, int id, [FromBody] SubstitutionDecisionBody body, CancellationToken ct)
    {
        if (!TryActor(out var actor, out var role)) return Unauthorized();
        var r = await _svc.DecideAsync(matchId, id, actor, role, body ?? new SubstitutionDecisionBody(false, null), ct);
        return r.Error == SubstitutionError.None ? Ok(r.Data) : Map(r.Error);
    }

    private IActionResult Err(int status, string code, string message) => StatusCode(status, new { code, message });

    private IActionResult Map(SubstitutionError e) => e switch
    {
        SubstitutionError.NotFound => Err(404, "NOT_FOUND", "Không tìm thấy trận hoặc yêu cầu thay người."),
        SubstitutionError.Forbidden => Err(403, "FORBIDDEN", "Bạn không có quyền xem thay người của trận này."),
        SubstitutionError.NotCaptain => Err(403, "NOT_CAPTAIN", "Chỉ đội trưởng của đội liên quan mới thực hiện được."),
        SubstitutionError.NotDecider => Err(403, "NOT_DECIDER", "Chỉ trọng tài của trận (hoặc BTC nếu trận không có trọng tài) mới duyệt được."),
        SubstitutionError.MatchNotEditable => Err(409, "MATCH_NOT_EDITABLE", "Trận không còn ở trạng thái sẵn sàng nên không thể thay người."),
        SubstitutionError.WindowClosed => Err(409, "WINDOW_CLOSED", "Đã quá giờ thi đấu, không thể gửi yêu cầu thay người."),
        SubstitutionError.NoRegistration => Err(409, "NO_REGISTRATION", "Đội chưa có đăng ký được duyệt trong giải này."),
        SubstitutionError.InvalidPlayers => Err(400, "PLAYERS_INVALID", "Người ra phải là tuyển thủ chính và người vào phải là dự bị hiện tại của đội."),
        SubstitutionError.PlayerBusy => Err(409, "PLAYER_BUSY", "Một trong hai tuyển thủ đang nằm trong yêu cầu thay người khác chờ duyệt."),
        SubstitutionError.QuotaExceeded => Err(409, "QUOTA_EXCEEDED", "Đã hết số lần thay người tối đa cho trận theo Rule giải."),
        SubstitutionError.InvalidReason => Err(400, "TEXT_TOO_LONG", "Lý do / ghi chú tối đa 300 ký tự."),
        SubstitutionError.NoteRequired => Err(400, "NOTE_REQUIRED", "Từ chối phải kèm ghi chú (tối thiểu 5 ký tự)."),
        SubstitutionError.NotPending => Err(409, "NOT_PENDING", "Yêu cầu này không còn ở trạng thái chờ duyệt."),
        _ => Err(500, "UNKNOWN", "Lỗi không xác định.")
    };
}