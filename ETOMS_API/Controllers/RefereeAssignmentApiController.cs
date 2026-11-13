using ETOMS_API.DTOs.Tournament;
using ETOMS_API.Services;
using ETOMS_API.Services.Interface;
using Microsoft.AspNetCore.Mvc;

namespace ETOMS_API.Controllers;

// API JSON cho TOUR-13 (UC-17): gán trọng tài. Chỉ Ban tổ chức của giải.
[Route("api/tournaments/{tournamentId:int}")]
public class RefereeAssignmentApiController : ApiControllerBase
{
    private readonly IRefereeAssignmentService _refereeService;
    private readonly ICurrentUser _currentUser;

    public RefereeAssignmentApiController(IRefereeAssignmentService refereeService, ICurrentUser currentUser)
    {
        _refereeService = refereeService;
        _currentUser = currentUser;
    }

    // GET /api/tournaments/{id}/referee-board
    [HttpGet("referee-board")]
    public async Task<IActionResult> GetBoard(int tournamentId)
    {
        var denied = await CheckOrganizerAsync(tournamentId);
        if (denied is not null) return denied;
        return Ok(await _refereeService.GetBoardAsync(tournamentId));
    }

    // GET /api/tournaments/{id}/referee-candidates?q=
    [HttpGet("referee-candidates")]
    public async Task<IActionResult> SearchCandidates(int tournamentId, [FromQuery] string? q)
    {
        var denied = await CheckOrganizerAsync(tournamentId);
        if (denied is not null) return denied;
        return Ok(await _refereeService.SearchCandidatesAsync(q));
    }

    // PUT /api/tournaments/{id}/matches/{matchId}/referee — refereeAccountId = null để bỏ trọng tài.
    [HttpPut("matches/{matchId:int}/referee")]
    public async Task<IActionResult> Assign(int tournamentId, int matchId, [FromBody] AssignRefereeRequest request)
    {
        var denied = await CheckOrganizerAsync(tournamentId);
        if (denied is not null) return denied;
        return FromResult(await _refereeService.AssignRefereeAsync(tournamentId, matchId, request.RefereeAccountId));
    }

    // POST /api/tournaments/{id}/referee-board/round — gán cả vòng.
    [HttpPost("referee-board/round")]
    public async Task<IActionResult> AssignRound(int tournamentId, [FromBody] AssignRoundRefereeRequest request)
    {
        var denied = await CheckOrganizerAsync(tournamentId);
        if (denied is not null) return denied;
        return FromResult(await _refereeService.AssignRoundAsync(tournamentId, request));
    }

    private async Task<IActionResult?> CheckOrganizerAsync(int tournamentId)
    {
        if (!_currentUser.IsAuthenticated) return Unauthorized();

        var organizerId = await _refereeService.GetOrganizerAccountIdAsync(tournamentId);
        if (organizerId is null) return NotFound();
        return organizerId == _currentUser.UserId!.Value ? null : StatusCode(403);
    }
}
