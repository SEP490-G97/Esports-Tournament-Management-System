using ETOMS_API.Services.Interface;
using Microsoft.AspNetCore.Mvc;

namespace ETOMS_API.Controllers;

// API JSON cho BRK-09: xem trận tranh hạng Ba của giải. Công khai như nhánh đấu.
// Việc tạo trận và đưa đội vào do ThirdPlaceWorker làm, không có nút bấm.
[Route("api/tournaments/{tournamentId:int}/third-place")]
public class ThirdPlaceApiController : ApiControllerBase
{
    private readonly IThirdPlaceService _thirdPlaceService;

    public ThirdPlaceApiController(IThirdPlaceService thirdPlaceService) => _thirdPlaceService = thirdPlaceService;

    // GET /api/tournaments/{id}/third-place
    [HttpGet]
    public async Task<IActionResult> Get(int tournamentId)
    {
        var status = await _thirdPlaceService.GetStatusAsync(tournamentId);
        return status is null ? NotFound() : Ok(new { message = string.Empty, data = status });
    }
}
