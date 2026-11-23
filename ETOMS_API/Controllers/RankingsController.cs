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
        // Điểm thành tích chỉ so được trong cùng một tựa game — không trả bảng trộn nhiều game.
        if (gameId is null)
        {
            return BadRequest(new { message = "Vui lòng chọn tựa game để xem bảng xếp hạng." });
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

    // Mùa giải của MỌI tựa game trong một lần hỏi (trang Bảng xếp hạng trước đây gọi /seasons cho từng game —
    // 24 game là 24 lượt đi về database ở xa chỉ để ghi chú "chưa có giải kết thúc").
    [HttpGet("seasons/by-game")]
    public async Task<IActionResult> GetSeasonsByGame(CancellationToken cancellationToken)
        => Ok(new
        {
            message = "Đã tải danh sách mùa giải.",
            data = await _rankingService.GetSeasonsByGameAsync(cancellationToken)
        });

    [HttpGet("teams/{teamId:int}")]
    public async Task<IActionResult> GetTeamProfile(int teamId, CancellationToken cancellationToken)
        => FromResult(await _rankingService.GetTeamProfileAsync(teamId, cancellationToken));
}
