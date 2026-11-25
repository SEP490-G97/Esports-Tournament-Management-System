using ETOMS_API.DTOs.Tournament;
using ETOMS_API.Services.Interface;
using Microsoft.AspNetCore.Mvc;

namespace ETOMS_API.Controllers;

// Mục 28: tiến trình giải ở bảng điều hành — chỉ Ban tổ chức của giải.
[Route("api/tournaments/{tournamentId:int}/lifecycle")]
public class TournamentLifecycleApiController : ApiControllerBase
{
    private readonly ITournamentLifecycleService _service;
    private readonly ICurrentUser _me;

    public TournamentLifecycleApiController(ITournamentLifecycleService service, ICurrentUser me)
    {
        _service = service;
        _me = me;
    }

    private async Task<IActionResult?> GuardAsync(int tournamentId)
    {
        if (!_me.IsAuthenticated || _me.UserId is null) return Unauthorized();
        var organizer = await _service.GetOrganizerAccountIdAsync(tournamentId);
        if (organizer is null) return NotFound(new { message = "Không tìm thấy giải đấu." });
        if (organizer != _me.UserId) return StatusCode(403, new { message = "Chỉ Ban tổ chức của giải được thao tác." });
        return null;
    }

    [HttpGet]
    public async Task<IActionResult> Get(int tournamentId)
    {
        if (await GuardAsync(tournamentId) is { } deny) return deny;
        Response.Headers.CacheControl = "no-store";
        return Ok(await _service.GetAsync(tournamentId));
    }

    [HttpPost("start")]
    public async Task<IActionResult> Start(int tournamentId)
    {
        if (await GuardAsync(tournamentId) is { } deny) return deny;
        return FromResult(await _service.StartAsync(tournamentId, _me.UserId!.Value));
    }

    [HttpPost("finish")]
    public async Task<IActionResult> Finish(int tournamentId, [FromBody] FinishTournamentRequest? request)
    {
        if (await GuardAsync(tournamentId) is { } deny) return deny;
        return FromResult(await _service.FinishAsync(tournamentId, _me.UserId!.Value, request?.TieOrder));
    }

    [HttpPost("extend")]
    public async Task<IActionResult> Extend(int tournamentId, [FromBody] ExtendRegistrationRequest request)
    {
        if (await GuardAsync(tournamentId) is { } deny) return deny;
        return FromResult(await _service.ExtendRegistrationAsync(tournamentId, _me.UserId!.Value, request));
    }
}
