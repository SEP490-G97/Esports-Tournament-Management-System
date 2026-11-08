using ETOMS_API.DTOs.GameRule;
using ETOMS_API.Services;
using ETOMS_API.Services.Interface;
using Microsoft.AspNetCore.Mvc;

namespace ETOMS_API.Controllers;

// API JSON cho GRL-03: vị trí trong đội của tựa game. Chỉ System Admin.
[Route("api/game-rules/{gameId:int}/positions")]
public class GamePositionsApiController : ApiControllerBase
{
    private readonly IGamePositionService _positionService;
    private readonly IGameRuleService _gameRuleService;
    private readonly ICurrentUser _currentUser;

    public GamePositionsApiController(IGamePositionService positionService, IGameRuleService gameRuleService, ICurrentUser currentUser)
    {
        _positionService = positionService;
        _gameRuleService = gameRuleService;
        _currentUser = currentUser;
    }

    // GET /api/game-rules/{gameId}/positions
    [HttpGet]
    public async Task<IActionResult> GetAll(int gameId)
    {
        var denied = await CheckAdminAsync();
        if (denied is not null) return denied;
        return FromResult(await _positionService.GetPositionsAsync(gameId));
    }

    // POST /api/game-rules/{gameId}/positions
    [HttpPost]
    public async Task<IActionResult> Add(int gameId, [FromBody] SaveGamePositionRequest request)
    {
        var denied = await CheckAdminAsync();
        if (denied is not null) return denied;
        return FromResult(await _positionService.AddPositionAsync(gameId, request));
    }

    // PUT /api/game-rules/{gameId}/positions/{positionId}
    [HttpPut("{positionId:int}")]
    public async Task<IActionResult> Update(int gameId, int positionId, [FromBody] SaveGamePositionRequest request)
    {
        var denied = await CheckAdminAsync();
        if (denied is not null) return denied;
        return FromResult(await _positionService.UpdatePositionAsync(gameId, positionId, request));
    }

    // POST /api/game-rules/{gameId}/positions/{positionId}/move — { direction: "up" | "down" }
    [HttpPost("{positionId:int}/move")]
    public async Task<IActionResult> Move(int gameId, int positionId, [FromBody] MoveGamePositionRequest request)
    {
        var denied = await CheckAdminAsync();
        if (denied is not null) return denied;
        return FromResult(await _positionService.MovePositionAsync(gameId, positionId, request.Direction));
    }

    private async Task<IActionResult?> CheckAdminAsync()
    {
        if (!_currentUser.IsAuthenticated) return Unauthorized();
        return await _gameRuleService.IsAdminAsync(_currentUser.UserId!.Value) ? null : StatusCode(403);
    }
}
