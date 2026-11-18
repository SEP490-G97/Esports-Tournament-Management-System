using ETOMS_API.DTOs.Bracket;
using ETOMS_API.Services;
using ETOMS_API.Services.Interface;
using Microsoft.AspNetCore.Mvc;

namespace ETOMS_API.Controllers;

// API JSON cho BRK-08: chuyển vòng bảng sang knockout. Chỉ Ban tổ chức của giải.
[Route("api/tournaments/{tournamentId:int}/stage-transition")]
public class StageTransitionApiController : ApiControllerBase
{
    private readonly IStageTransitionService _transitionService;
    private readonly ICurrentUser _currentUser;

    public StageTransitionApiController(IStageTransitionService transitionService, ICurrentUser currentUser)
    {
        _transitionService = transitionService;
        _currentUser = currentUser;
    }

    // GET /api/tournaments/{id}/stage-transition
    [HttpGet]
    public async Task<IActionResult> Get(int tournamentId)
    {
        var denied = await CheckOrganizerAsync(tournamentId);
        if (denied is not null) return denied;
        return FromResult(await _transitionService.GetBoardAsync(tournamentId));
    }

    // POST /api/tournaments/{id}/stage-transition/preview — xem trước với thứ tự đội hòa đang chọn, không lưu.
    [HttpPost("preview")]
    public async Task<IActionResult> Preview(int tournamentId, [FromBody] AdvanceStageRequest request)
    {
        var denied = await CheckOrganizerAsync(tournamentId);
        if (denied is not null) return denied;
        return FromResult(await _transitionService.GetBoardAsync(tournamentId, request));
    }

    // POST /api/tournaments/{id}/stage-transition — tạo nhánh knockout.
    [HttpPost]
    public async Task<IActionResult> Advance(int tournamentId, [FromBody] AdvanceStageRequest request)
    {
        var denied = await CheckOrganizerAsync(tournamentId);
        if (denied is not null) return denied;
        return FromResult(await _transitionService.AdvanceAsync(tournamentId, request));
    }

    private async Task<IActionResult?> CheckOrganizerAsync(int tournamentId)
    {
        if (!_currentUser.IsAuthenticated) return Unauthorized();

        var organizerId = await _transitionService.GetOrganizerAccountIdAsync(tournamentId);
        if (organizerId is null) return NotFound();
        return organizerId == _currentUser.UserId!.Value ? null : StatusCode(403);
    }
}
