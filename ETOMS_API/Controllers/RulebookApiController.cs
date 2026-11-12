using ETOMS_API.DTOs.Tournament;
using ETOMS_API.Services;
using ETOMS_API.Services.Interface;
using Microsoft.AspNetCore.Mvc;

namespace ETOMS_API.Controllers;

// API JSON cho TOUR-08 (UC-13): soạn thể lệ giải và xem các phiên bản đã lưu.
[Route("api/tournaments/{tournamentId:int}/rulebook")]
public class RulebookApiController : ApiControllerBase
{
    private readonly IRulebookService _rulebookService;
    private readonly ICurrentUser _currentUser;

    public RulebookApiController(IRulebookService rulebookService, ICurrentUser currentUser)
    {
        _rulebookService = rulebookService;
        _currentUser = currentUser;
    }

    // GET /api/tournaments/{id}/rulebook?version= — bỏ trống version để lấy bản hiện hành.
    [HttpGet]
    public async Task<IActionResult> Get(int tournamentId, [FromQuery] int? version)
    {
        var rulebook = await _rulebookService.GetRulebookAsync(tournamentId, version, _currentUser.UserId);
        return rulebook is null ? NotFound() : Ok(rulebook);
    }

    // GET /api/tournaments/{id}/rulebook/versions
    [HttpGet("versions")]
    public async Task<IActionResult> GetVersions(int tournamentId)
    {
        var versions = await _rulebookService.GetVersionsAsync(tournamentId, _currentUser.UserId);
        return versions is null ? NotFound() : Ok(versions);
    }

    // PUT /api/tournaments/{id}/rulebook — chỉ Ban tổ chức của giải.
    [HttpPut]
    public async Task<IActionResult> Save(int tournamentId, [FromBody] SaveRulebookRequest request)
    {
        if (!_currentUser.IsAuthenticated) return Unauthorized();

        var isOrganizer = await _rulebookService.IsOrganizerAsync(tournamentId, _currentUser.UserId!.Value);
        if (isOrganizer is null) return NotFound();
        if (isOrganizer == false) return StatusCode(403);

        return FromResult(await _rulebookService.SaveRulebookAsync(tournamentId, request, _currentUser.UserId.Value));
    }
}
