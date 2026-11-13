using ETOMS_API.DTOs.Tournament;
using ETOMS_API.Services;
using ETOMS_API.Services.Interface;
using Microsoft.AspNetCore.Mvc;

namespace ETOMS_API.Controllers;

// API JSON cho GRL-06: chọn bộ luật cho giải. Chỉ Ban tổ chức của giải.
[Route("api/tournaments/{tournamentId:int}/ruleset")]
public class TournamentRulesetApiController : ApiControllerBase
{
    private readonly ITournamentRulesetService _rulesetService;
    private readonly ICurrentUser _currentUser;

    public TournamentRulesetApiController(ITournamentRulesetService rulesetService, ICurrentUser currentUser)
    {
        _rulesetService = rulesetService;
        _currentUser = currentUser;
    }

    // GET /api/tournaments/{id}/ruleset
    [HttpGet]
    public async Task<IActionResult> Get(int tournamentId)
    {
        var denied = await CheckOrganizerAsync(tournamentId);
        if (denied is not null) return denied;
        return FromResult(await _rulesetService.GetBoardAsync(tournamentId));
    }

    // PUT /api/tournaments/{id}/ruleset — { gameRulesetId }
    [HttpPut]
    public async Task<IActionResult> Select(int tournamentId, [FromBody] SelectTournamentRulesetRequest request)
    {
        var denied = await CheckOrganizerAsync(tournamentId);
        if (denied is not null) return denied;
        return FromResult(await _rulesetService.SelectRulesetAsync(tournamentId, request.GameRulesetId));
    }

    private async Task<IActionResult?> CheckOrganizerAsync(int tournamentId)
    {
        if (!_currentUser.IsAuthenticated) return Unauthorized();

        var organizerId = await _rulesetService.GetOrganizerAccountIdAsync(tournamentId);
        if (organizerId is null) return NotFound();
        return organizerId == _currentUser.UserId!.Value ? null : StatusCode(403);
    }
}
