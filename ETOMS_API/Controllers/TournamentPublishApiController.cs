using ETOMS_API.Services;
using ETOMS_API.Services.Interface;
using Microsoft.AspNetCore.Mvc;

namespace ETOMS_API.Controllers;

// API JSON cho TOUR-09 (UC-14): xuất bản và gỡ xuất bản giải. Chỉ Ban tổ chức của giải.
[Route("api/tournaments/{tournamentId:int}")]
public class TournamentPublishApiController : ApiControllerBase
{
    private readonly ITournamentPublishService _publishService;
    private readonly ICurrentUser _currentUser;

    public TournamentPublishApiController(ITournamentPublishService publishService, ICurrentUser currentUser)
    {
        _publishService = publishService;
        _currentUser = currentUser;
    }

    // GET /api/tournaments/{id}/publish-status
    [HttpGet("publish-status")]
    public async Task<IActionResult> GetStatus(int tournamentId)
    {
        var denied = await CheckOrganizerAsync(tournamentId);
        if (denied is not null) return denied;
        return Ok(await _publishService.GetPublishStatusAsync(tournamentId));
    }

    // POST /api/tournaments/{id}/publish
    [HttpPost("publish")]
    public async Task<IActionResult> Publish(int tournamentId)
    {
        var denied = await CheckOrganizerAsync(tournamentId);
        if (denied is not null) return denied;
        return FromResult(await _publishService.PublishTournamentAsync(tournamentId, _currentUser.UserId!.Value));
    }

    // POST /api/tournaments/{id}/unpublish
    [HttpPost("unpublish")]
    public async Task<IActionResult> Unpublish(int tournamentId)
    {
        var denied = await CheckOrganizerAsync(tournamentId);
        if (denied is not null) return denied;
        return FromResult(await _publishService.UnpublishTournamentAsync(tournamentId, _currentUser.UserId!.Value));
    }

    private async Task<IActionResult?> CheckOrganizerAsync(int tournamentId)
    {
        if (!_currentUser.IsAuthenticated) return Unauthorized();

        var isOrganizer = await _publishService.IsOrganizerAsync(tournamentId, _currentUser.UserId!.Value);
        if (isOrganizer is null) return NotFound();
        return isOrganizer == true ? null : StatusCode(403);
    }
}
