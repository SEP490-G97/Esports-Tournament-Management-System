using ETOMS_API.DTOs.GameRule;
using ETOMS_API.Services;
using ETOMS_API.Services.Interface;
using Microsoft.AspNetCore.Mvc;

namespace ETOMS_API.Controllers;

// API JSON cho GRL-01: luật thi đấu của từng tựa game. Chỉ System Admin.
[Route("api/game-rules")]
public class GameRulesApiController : ApiControllerBase
{
    private readonly IGameRuleService _gameRuleService;
    private readonly ICurrentUser _currentUser;

    public GameRulesApiController(IGameRuleService gameRuleService, ICurrentUser currentUser)
    {
        _gameRuleService = gameRuleService;
        _currentUser = currentUser;
    }

    // GET /api/game-rules
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var denied = await CheckAdminAsync();
        if (denied is not null) return denied;
        return Ok(await _gameRuleService.GetRulesAsync());
    }

    // PUT /api/game-rules/{gameId}
    [HttpPut("{gameId:int}")]
    public async Task<IActionResult> Update(int gameId, [FromBody] UpdateGameRuleRequest request)
    {
        var denied = await CheckAdminAsync();
        if (denied is not null) return denied;
        return FromResult(await _gameRuleService.UpdateRuleAsync(gameId, request));
    }

    private async Task<IActionResult?> CheckAdminAsync()
    {
        if (!_currentUser.IsAuthenticated) return Unauthorized();
        return await _gameRuleService.IsAdminAsync(_currentUser.UserId!.Value) ? null : StatusCode(403);
    }
}
