using ETOMS_API.Services.Interface;
using Microsoft.AspNetCore.Mvc;

namespace ETOMS_API.Controllers;

[Route("api/rankings")]
public sealed class RankingsController : ApiControllerBase
{
    private readonly IRankingService _rankingService;

    public RankingsController(IRankingService rankingService)
    {
        _rankingService = rankingService;
    }

    [HttpGet]
    public async Task<IActionResult> GetLeaderboard(
        [FromQuery] int? gameId,
        [FromQuery] int? season,
        CancellationToken cancellationToken)
    {
        if (gameId is <= 0 || season is <= 0)
        {
            return BadRequest(new { message = "Bộ lọc tựa game và mùa giải phải là số dương." });
        }

        return Ok(new
        {
            message = "Đã tải bảng xếp hạng.",
            data = await _rankingService.GetLeaderboardAsync(gameId, season, cancellationToken)
        });
    }

    [HttpGet("seasons")]
    public async Task<IActionResult> GetSeasons(
        [FromQuery] int? gameId,
        CancellationToken cancellationToken)
    {
        if (gameId is <= 0)
        {
            return BadRequest(new { message = "Mã tựa game phải là số dương." });
        }

        return Ok(new
        {
            message = "Đã tải danh sách mùa giải.",
            data = await _rankingService.GetSeasonsAsync(gameId, cancellationToken)
        });
    }

    [HttpGet("teams/{teamId:int}")]
    public async Task<IActionResult> GetTeamProfile(int teamId, CancellationToken cancellationToken)
        => FromResult(await _rankingService.GetTeamProfileAsync(teamId, cancellationToken));
}
