using ETOMS_API.Services;
using ETOMS_API.Services.Interface;
using Microsoft.AspNetCore.Mvc;

namespace ETOMS_API.Controllers;

// API JSON cho BRK-13: bảng xếp hạng vòng bảng. Công khai; giải còn nháp chỉ Ban tổ chức xem được.
[Route("api/tournaments/{tournamentId:int}/standings")]
public class GroupStandingApiController : ApiControllerBase
{
    private readonly IGroupStandingService _standingService;
    private readonly ICurrentUser _currentUser;

    public GroupStandingApiController(IGroupStandingService standingService, ICurrentUser currentUser)
    {
        _standingService = standingService;
        _currentUser = currentUser;
    }

    // GET /api/tournaments/{id}/standings
    [HttpGet]
    public async Task<IActionResult> Get(int tournamentId)
    {
        var board = await _standingService.GetStandingsAsync(tournamentId, _currentUser.IsAuthenticated ? _currentUser.UserId : null);
        return board is null ? NotFound() : Ok(new { message = string.Empty, data = board });
    }
}
