using ETOMS_API.DTOs.Tournament;
using ETOMS_API.Services;
using ETOMS_API.Services.Interface;
using Microsoft.AspNetCore.Mvc;

namespace ETOMS_API.Controllers;

// API JSON cho TOUR-10: sửa thông tin giải. Chỉ Ban tổ chức của giải.
[Route("api/tournaments/{tournamentId:int}/settings")]
public class TournamentSettingsApiController : ApiControllerBase
{
    private readonly ITournamentSettingsService _settingsService;
    private readonly ICurrentUser _currentUser;

    public TournamentSettingsApiController(ITournamentSettingsService settingsService, ICurrentUser currentUser)
    {
        _settingsService = settingsService;
        _currentUser = currentUser;
    }

    // GET /api/tournaments/{id}/settings
    [HttpGet]
    public async Task<IActionResult> Get(int tournamentId)
    {
        var denied = await CheckOrganizerAsync(tournamentId);
        if (denied is not null) return denied;
        var board = await _settingsService.GetSettingsAsync(tournamentId);
        return board is null ? NotFound() : Ok(new { message = string.Empty, data = board });
    }

    // PUT /api/tournaments/{id}/settings — toàn bộ giá trị của form
    [HttpPut]
    public async Task<IActionResult> Save(int tournamentId, [FromBody] TournamentSettingsValues request)
    {
        var denied = await CheckOrganizerAsync(tournamentId);
        if (denied is not null) return denied;
        return FromResult(await _settingsService.SaveSettingsAsync(tournamentId, request));
    }

    private async Task<IActionResult?> CheckOrganizerAsync(int tournamentId)
    {
        if (!_currentUser.IsAuthenticated) return Unauthorized();

        var organizerId = await _settingsService.GetOrganizerAccountIdAsync(tournamentId);
        if (organizerId is null) return NotFound();
        return organizerId == _currentUser.UserId!.Value ? null : StatusCode(403);
    }
}
