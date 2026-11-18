using ETOMS_API.Services;
using ETOMS_API.Services.Interface;
using Microsoft.AspNetCore.Mvc;

namespace ETOMS_API.Controllers;

// API JSON cho TOUR-12: bảng điều khiển của một giải, chỉ Ban tổ chức của giải đó.
[Route("api/tournaments/{tournamentId:int}/dashboard")]
public class TournamentDashboardApiController : ApiControllerBase
{
    private readonly ITournamentDashboardService _dashboardService;
    private readonly ICurrentUser _currentUser;

    public TournamentDashboardApiController(ITournamentDashboardService dashboardService, ICurrentUser currentUser)
    {
        _dashboardService = dashboardService;
        _currentUser = currentUser;
    }

    // GET /api/tournaments/{id}/dashboard
    [HttpGet]
    public async Task<IActionResult> Get(int tournamentId)
    {
        if (!_currentUser.IsAuthenticated) return Unauthorized();

        var organizerId = await _dashboardService.GetOrganizerAccountIdAsync(tournamentId);
        if (organizerId is null) return NotFound();
        if (organizerId != _currentUser.UserId!.Value) return StatusCode(403);

        return Ok(await _dashboardService.GetDashboardAsync(tournamentId));
    }
}
