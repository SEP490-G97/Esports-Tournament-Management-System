using ETOMS_API.DTOs.Bracket;
using ETOMS_API.Services.Interface;
using Microsoft.AspNetCore.Mvc;

namespace ETOMS_API.Controllers;

// API JSON cho module BRK (BRK-01,02,03,04,06) — song song với BracketController (MVC).
// GetTree công khai (Guest xem được, giống BRK-10 bản MVC). Generate/Lock chỉ organizer.
//
// KHÔNG đưa PickWinner (BracketController.PickWinner, MVC) lên API: đó chỉ là cầu nối tạm
// để demo BRK trong lúc module kết quả chưa code — theo LI-01, tỉ số CHỈ được vào hệ thống
// qua MatchReport (hai báo cáo mù độc lập) hoặc phán quyết VAR trong Dispute. Cầu nối này sẽ
// bị xóa khi có module đó, nên không công bố thành endpoint chính thức.
[Route("api/tournaments/{tournamentId:int}/bracket")]
public class BracketsApiController : ApiControllerBase
{
    private readonly IBracketService _bracketService;
    private readonly ICurrentUser _currentUser;

    public BracketsApiController(IBracketService bracketService, ICurrentUser currentUser)
    {
        _bracketService = bracketService;
        _currentUser = currentUser;
    }

    // GET /api/tournaments/{tournamentId}/bracket — công khai.
    [HttpGet]
    public async Task<IActionResult> GetTree(int tournamentId)
    {
        var tree = await _bracketService.GetBracketAsync(tournamentId);
        return tree is null ? NotFound() : Ok(tree);
    }

    // GET /api/tournaments/{tournamentId}/bracket/approved-teams — chỉ organizer (dùng cho màn xếp hạt giống).
    [HttpGet("approved-teams")]
    public async Task<IActionResult> GetApprovedTeams(int tournamentId)
    {
        if (!_currentUser.IsAuthenticated) return Unauthorized();
        if (!await _bracketService.IsOrganizerAsync(tournamentId, _currentUser.UserId!.Value)) return StatusCode(403);
        return Ok(await _bracketService.GetApprovedTeamsAsync(tournamentId));
    }

    // POST /api/tournaments/{tournamentId}/bracket/generate — BRK-01+02+04+06.
    [HttpPost("generate")]
    public async Task<IActionResult> Generate(int tournamentId, [FromBody] GenerateBracketRequest model)
    {
        if (!_currentUser.IsAuthenticated) return Unauthorized();
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        return FromResult(await _bracketService.GenerateBracketAsync(tournamentId, model, _currentUser.UserId!.Value));
    }

    // POST /api/tournaments/{tournamentId}/bracket/lock — BRK-03.
    [HttpPost("lock")]
    public async Task<IActionResult> Lock(int tournamentId)
    {
        if (!_currentUser.IsAuthenticated) return Unauthorized();
        return FromResult(await _bracketService.LockBracketAsync(tournamentId, _currentUser.UserId!.Value));
    }

    // GET /api/tournaments/{tournamentId}/bracket/tiebreaker — BRK-07, công khai (hiện cho mọi người xem).
    [HttpGet("tiebreaker")]
    public async Task<IActionResult> GetTieBreaker(int tournamentId)
    {
        var config = await _bracketService.GetTieBreakerConfigAsync(tournamentId);
        return config is null ? NotFound() : Ok(config);
    }

    // PUT /api/tournaments/{tournamentId}/bracket/tiebreaker — BRK-07, chỉ organizer.
    [HttpPut("tiebreaker")]
    public async Task<IActionResult> SetTieBreaker(int tournamentId, [FromBody] SetTieBreakerRequest model)
    {
        if (!_currentUser.IsAuthenticated) return Unauthorized();
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        return FromResult(await _bracketService.SetTieBreakerConfigAsync(tournamentId, model.Order, _currentUser.UserId!.Value));
    }
}
