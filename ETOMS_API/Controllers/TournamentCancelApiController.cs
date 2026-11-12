using ETOMS_API.DTOs.Tournament;
using ETOMS_API.Services;
using ETOMS_API.Services.Interface;
using Microsoft.AspNetCore.Mvc;

namespace ETOMS_API.Controllers;

// API JSON cho TOUR-11: hủy giải. Chỉ Ban tổ chức của giải.
[Route("api/tournaments/{tournamentId:int}/cancel")]
public class TournamentCancelApiController : ApiControllerBase
{
    private readonly ITournamentCancelService _cancelService;
    private readonly ICurrentUser _currentUser;

    public TournamentCancelApiController(ITournamentCancelService cancelService, ICurrentUser currentUser)
    {
        _cancelService = cancelService;
        _currentUser = currentUser;
    }

    // GET /api/tournaments/{id}/cancel — điều kiện hủy, số đội trưởng sẽ được báo, danh sách cần hoàn lệ phí
    [HttpGet]
    public async Task<IActionResult> Get(int tournamentId)
    {
        var denied = await CheckOrganizerAsync(tournamentId);
        if (denied is not null) return denied;
        var board = await _cancelService.GetCancelBoardAsync(tournamentId);
        return board is null ? NotFound() : Ok(new { message = string.Empty, data = board });
    }

    // POST /api/tournaments/{id}/cancel — { reason }
    [HttpPost]
    public async Task<IActionResult> Cancel(int tournamentId, [FromBody] CancelTournamentRequest request)
    {
        var denied = await CheckOrganizerAsync(tournamentId);
        if (denied is not null) return denied;
        return FromResult(await _cancelService.CancelAsync(tournamentId, _currentUser.UserId!.Value, request.Reason));
    }

    private async Task<IActionResult?> CheckOrganizerAsync(int tournamentId)
    {
        if (!_currentUser.IsAuthenticated) return Unauthorized();

        var organizerId = await _cancelService.GetOrganizerAccountIdAsync(tournamentId);
        if (organizerId is null) return NotFound();
        return organizerId == _currentUser.UserId!.Value ? null : StatusCode(403);
    }
}
