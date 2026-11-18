using ETOMS_API.DTOs.Registration;
using ETOMS_API.Services;
using ETOMS_API.Services.Interface;
using Microsoft.AspNetCore.Mvc;

namespace ETOMS_API.Controllers;

// API JSON cho GRL-04: vị trí của từng người trong đội hình đăng ký. Chỉ đội trưởng của đội.
[Route("api/registrations/{registrationId:int}/lineup-positions")]
public class LineupPositionsApiController : ApiControllerBase
{
    private readonly ILineupPositionService _lineupService;
    private readonly ICurrentUser _currentUser;

    public LineupPositionsApiController(ILineupPositionService lineupService, ICurrentUser currentUser)
    {
        _lineupService = lineupService;
        _currentUser = currentUser;
    }

    // GET /api/registrations/{id}/lineup-positions
    [HttpGet]
    public async Task<IActionResult> Get(int registrationId)
    {
        var denied = await CheckCaptainAsync(registrationId);
        if (denied is not null) return denied;
        return FromResult(await _lineupService.GetBoardAsync(registrationId));
    }

    // PUT /api/registrations/{id}/lineup-positions
    [HttpPut]
    public async Task<IActionResult> Save(int registrationId, [FromBody] SaveLineupPositionsRequest request)
    {
        var denied = await CheckCaptainAsync(registrationId);
        if (denied is not null) return denied;
        return FromResult(await _lineupService.SavePositionsAsync(registrationId, request));
    }

    private async Task<IActionResult?> CheckCaptainAsync(int registrationId)
    {
        if (!_currentUser.IsAuthenticated) return Unauthorized();

        var captainId = await _lineupService.GetCaptainAccountIdAsync(registrationId);
        if (captainId is null) return NotFound();
        return captainId == _currentUser.UserId!.Value ? null : StatusCode(403);
    }
}
