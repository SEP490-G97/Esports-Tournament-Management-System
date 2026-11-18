using System.Security.Claims;
using ETOMS_API.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ETOMS_API.Controllers;

/// <summary>Chi tiết trận đấu — công khai; người đã đăng nhập thấy thêm thông tin và nút thao tác theo vai trò.</summary>
[ApiController]
[AllowAnonymous]
[Route("api/matches")]
public class MatchesController : ControllerBase
{
    private readonly IMatchDetailService _svc;

    public MatchesController(IMatchDetailService svc) => _svc = svc;

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var r = await _svc.GetAsync(id, CurrentViewer(), ct);
        if (r.Error == MatchDetailError.NotFound)
            return NotFound(new { code = "MATCH_NOT_FOUND", message = "Không tìm thấy trận đấu." });

        // Nội dung phụ thuộc người xem → không cho cache chung.
        Response.Headers.CacheControl = "private, no-store";
        return Ok(r.Data);
    }

    private MatchViewer CurrentViewer()
    {
        if (User.Identity?.IsAuthenticated != true) return MatchViewer.Guest;
        var raw = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(raw, out var id) || id <= 0) return MatchViewer.Guest;
        return new MatchViewer(id, User.FindFirstValue("role") ?? User.FindFirstValue(ClaimTypes.Role));
    }
}