using ETOMS_API.Services;
using ETOMS_API.Services.Interface;
using Microsoft.AspNetCore.Mvc;

namespace ETOMS_API.Controllers;

// API JSON cho BRK-10, BRK-11: nhánh đấu công khai. Không cần đăng nhập (UC-20).
// Đường dẫn khác api/tournaments/{id}/bracket của BracketsApiController (BRK-01..03) để không trùng.
[Route("api/tournaments/{tournamentId:int}/bracket-view")]
public class BracketViewApiController : ApiControllerBase
{
    private readonly IBracketViewService _viewService;
    private readonly ICurrentUser _currentUser;

    public BracketViewApiController(IBracketViewService viewService, ICurrentUser currentUser)
    {
        _viewService = viewService;
        _currentUser = currentUser;
    }

    // GET /api/tournaments/{id}/bracket-view
    [HttpGet]
    public async Task<IActionResult> Get(int tournamentId)
    {
        var view = await _viewService.GetBracketAsync(tournamentId, _currentUser.IsAuthenticated ? _currentUser.UserId : null);
        return view is null ? NotFound() : Ok(new { message = string.Empty, data = view });
    }
}
