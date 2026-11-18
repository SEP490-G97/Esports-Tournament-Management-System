using ETOMS_API.DTOs.Tournament;
using ETOMS_API.Services;
using ETOMS_API.Services.Interface;
using Microsoft.AspNetCore.Mvc;

namespace ETOMS_API.Controllers;

// API JSON cho TOUR-14: nhân bản giải. Chỉ Ban tổ chức của giải gốc.
[Route("api/tournaments/{tournamentId:int}/clone")]
public class TournamentCloneApiController : ApiControllerBase
{
    private readonly ITournamentCloneService _cloneService;
    private readonly ICurrentUser _currentUser;

    public TournamentCloneApiController(ITournamentCloneService cloneService, ICurrentUser currentUser)
    {
        _cloneService = cloneService;
        _currentUser = currentUser;
    }

    // POST /api/tournaments/{id}/clone
    [HttpPost]
    public async Task<IActionResult> Clone(int tournamentId, [FromBody] CloneTournamentRequest request)
    {
        if (!_currentUser.IsAuthenticated) return Unauthorized();

        var organizerId = await _cloneService.GetOrganizerAccountIdAsync(tournamentId);
        if (organizerId is null) return NotFound();
        if (organizerId != _currentUser.UserId!.Value) return StatusCode(403);

        return FromResult(await _cloneService.CloneTournamentAsync(tournamentId, request));
    }
}
