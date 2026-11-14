using ETOMS_API.DTOs.Bracket;
using ETOMS_API.Services;
using ETOMS_API.Services.Interface;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace ETOMS_API.Controllers;

// API JSON cho BRK-13: bảng xếp hạng vòng bảng. Công khai; giải còn nháp chỉ Ban tổ chức xem được.
[Route("api/tournaments/{tournamentId:int}/standings")]
public class GroupStandingApiController : ApiControllerBase
{
    private readonly IGroupStandingService _standingService;
    private readonly ICurrentUser _currentUser;
    private readonly IMemoryCache _cache;

    public GroupStandingApiController(IGroupStandingService standingService, ICurrentUser currentUser, IMemoryCache cache)
    {
        _standingService = standingService;
        _currentUser = currentUser;
        _cache = cache;
    }

    // GET /api/tournaments/{id}/standings — cache RAM 20 giây cho khách chưa đăng nhập (database ở
    // xa, nhiều người cùng xem 1 giải). Giải còn DRAFT chỉ Ban tổ chức xem được — nếu cache theo
    // tournamentId chung cho mọi người thì có thể lộ dữ liệu đó, nên người đã đăng nhập luôn tính
    // lại, không đọc/ghi cache (xem lý do giống hệt ở BracketViewApiController).
    [HttpGet]
    public async Task<IActionResult> Get(int tournamentId)
    {
        var cacheKey = $"standings:{tournamentId}";
        if (!_currentUser.IsAuthenticated && _cache.TryGetValue(cacheKey, out GroupStandingBoardDto? cached))
            return Ok(new { message = string.Empty, data = cached });

        var board = await _standingService.GetStandingsAsync(tournamentId, _currentUser.IsAuthenticated ? _currentUser.UserId : null);
        if (board is null) return NotFound();

        if (!_currentUser.IsAuthenticated)
            _cache.Set(cacheKey, board, TimeSpan.FromSeconds(20));

        return Ok(new { message = string.Empty, data = board });
    }
}
