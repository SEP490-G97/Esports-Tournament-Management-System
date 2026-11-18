using ETOMS_API.DTOs.Bracket;
using ETOMS_API.Services;
using ETOMS_API.Services.Interface;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace ETOMS_API.Controllers;

// API JSON cho BRK-10, BRK-11: nhánh đấu công khai. Không cần đăng nhập (UC-20).
// Đường dẫn khác api/tournaments/{id}/bracket của BracketsApiController (BRK-01..03) để không trùng.
[Route("api/tournaments/{tournamentId:int}/bracket-view")]
public class BracketViewApiController : ApiControllerBase
{
    private readonly IBracketViewService _viewService;
    private readonly ICurrentUser _currentUser;
    private readonly IMemoryCache _cache;

    public BracketViewApiController(IBracketViewService viewService, ICurrentUser currentUser, IMemoryCache cache)
    {
        _viewService = viewService;
        _currentUser = currentUser;
        _cache = cache;
    }

    // GET /api/tournaments/{id}/bracket-view — cache RAM 20 giây cho KHÁCH CHƯA ĐĂNG NHẬP (database
    // ở xa, trang này nhiều người cùng xem 1 giải). Chỉ cache/dùng cache cho khách ẩn danh: nội dung
    // có thể khác cho Ban tổ chức (xem bản xem trước nhánh chưa khóa, hoặc xem được giải DRAFT) —
    // nếu lỡ cache nhầm bản đó rồi trả cho người khác thì lộ dữ liệu chưa công bố, nên người đã đăng
    // nhập luôn tính lại, không đọc/ghi cache.
    [HttpGet]
    public async Task<IActionResult> Get(int tournamentId)
    {
        var cacheKey = $"bracket-view:{tournamentId}";
        if (!_currentUser.IsAuthenticated && _cache.TryGetValue(cacheKey, out BracketViewDto? cached))
            return Ok(new { message = string.Empty, data = cached });

        var view = await _viewService.GetBracketAsync(tournamentId, _currentUser.IsAuthenticated ? _currentUser.UserId : null);
        if (view is null) return NotFound();

        if (!_currentUser.IsAuthenticated)
            _cache.Set(cacheKey, view, TimeSpan.FromSeconds(20));

        return Ok(new { message = string.Empty, data = view });
    }
}
