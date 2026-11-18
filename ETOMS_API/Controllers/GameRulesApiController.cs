using ETOMS_API.DTOs.GameRule;
using ETOMS_API.Services;
using ETOMS_API.Services.Interface;
using Microsoft.AspNetCore.Mvc;

namespace ETOMS_API.Controllers;

// API JSON cho GRL-01: các bộ luật thi đấu của từng tựa game. Chỉ System Admin.
public class GameRulesApiController : ApiControllerBase
{
    private readonly IGameRuleService _gameRuleService;
    private readonly ICurrentUser _currentUser;

    public GameRulesApiController(IGameRuleService gameRuleService, ICurrentUser currentUser)
    {
        _gameRuleService = gameRuleService;
        _currentUser = currentUser;
    }

    // GET /api/game-rules — mọi tựa game kèm bộ luật.
    [HttpGet("api/game-rules")]
    public async Task<IActionResult> GetBoard()
    {
        var denied = await CheckAdminAsync();
        if (denied is not null) return denied;
        return Ok(await _gameRuleService.GetBoardAsync());
    }

    // POST /api/game-rules/{gameId}/rulesets
    [HttpPost("api/game-rules/{gameId:int}/rulesets")]
    public async Task<IActionResult> Create(int gameId, [FromBody] SaveGameRulesetRequest request)
    {
        var denied = await CheckAdminAsync();
        if (denied is not null) return denied;
        return FromResult(await _gameRuleService.CreateRulesetAsync(gameId, request));
    }

    // PUT /api/game-rulesets/{rulesetId}
    [HttpPut("api/game-rulesets/{rulesetId:int}")]
    public async Task<IActionResult> Update(int rulesetId, [FromBody] SaveGameRulesetRequest request)
    {
        var denied = await CheckAdminAsync();
        if (denied is not null) return denied;
        return FromResult(await _gameRuleService.UpdateRulesetAsync(rulesetId, request));
    }

    private async Task<IActionResult?> CheckAdminAsync()
    {
        if (!_currentUser.IsAuthenticated) return Unauthorized();
        return await _gameRuleService.IsAdminAsync(_currentUser.UserId!.Value) ? null : StatusCode(403);
    }
}
