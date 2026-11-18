using ETOMS_API.DTOs.Bracket;
using ETOMS_API.Services;
using ETOMS_API.Services.Interface;
using Microsoft.AspNetCore.Mvc;

namespace ETOMS_API.Controllers;

// API JSON cho BRK-12: xếp lịch thi đấu. Chỉ Ban tổ chức của giải.
[Route("api/tournaments/{tournamentId:int}/schedule")]
public class MatchScheduleApiController : ApiControllerBase
{
    private readonly IMatchScheduleService _scheduleService;
    private readonly ICurrentUser _currentUser;

    public MatchScheduleApiController(IMatchScheduleService scheduleService, ICurrentUser currentUser)
    {
        _scheduleService = scheduleService;
        _currentUser = currentUser;
    }

    // GET /api/tournaments/{id}/schedule
    [HttpGet]
    public async Task<IActionResult> Get(int tournamentId)
    {
        var denied = await CheckOrganizerAsync(tournamentId);
        if (denied is not null) return denied;
        var board = await _scheduleService.GetBoardAsync(tournamentId);
        return board is null ? NotFound() : Ok(new { message = string.Empty, data = board });
    }

    // PUT /api/tournaments/{id}/schedule/matches/{matchId} — { scheduledAt } (null = bỏ giờ)
    [HttpPut("matches/{matchId:int}")]
    public async Task<IActionResult> ScheduleMatch(int tournamentId, int matchId, [FromBody] ScheduleMatchRequest request)
    {
        var denied = await CheckOrganizerAsync(tournamentId);
        if (denied is not null) return denied;
        return FromResult(await _scheduleService.ScheduleMatchAsync(tournamentId, matchId, request.ScheduledAt));
    }

    // POST /api/tournaments/{id}/schedule/round — { stageNo, roundNo, firstAt, gapMinutes }
    [HttpPost("round")]
    public async Task<IActionResult> ScheduleRound(int tournamentId, [FromBody] ScheduleRoundRequest request)
    {
        var denied = await CheckOrganizerAsync(tournamentId);
        if (denied is not null) return denied;
        return FromResult(await _scheduleService.ScheduleRoundAsync(tournamentId, request));
    }

    private async Task<IActionResult?> CheckOrganizerAsync(int tournamentId)
    {
        if (!_currentUser.IsAuthenticated) return Unauthorized();

        var organizerId = await _scheduleService.GetOrganizerAccountIdAsync(tournamentId);
        if (organizerId is null) return NotFound();
        return organizerId == _currentUser.UserId!.Value ? null : StatusCode(403);
    }
}
