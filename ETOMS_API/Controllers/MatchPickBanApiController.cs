using ETOMS_API.DTOs.PickBan;
using ETOMS_API.Services;
using ETOMS_API.Services.Interface;
using Microsoft.AspNetCore.Mvc;

namespace ETOMS_API.Controllers;

// API JSON cho GRL-07: kết quả cấm/chọn bản đồ của trận.
// Xem công khai; chỉ trọng tài của trận hoặc Ban tổ chức của giải được nhập.
[Route("api/matches/{matchId:int}/pick-ban")]
public class MatchPickBanApiController : ApiControllerBase
{
    private readonly IMatchPickBanService _pickBanService;
    private readonly ICurrentUser _currentUser;

    public MatchPickBanApiController(IMatchPickBanService pickBanService, ICurrentUser currentUser)
    {
        _pickBanService = pickBanService;
        _currentUser = currentUser;
    }

    // GET /api/matches/{id}/pick-ban
    [HttpGet]
    public async Task<IActionResult> Get(int matchId)
    {
        var result = await _pickBanService.GetBoardAsync(matchId, _currentUser.IsAuthenticated ? _currentUser.UserId : null);
        if (!result.IsSuccess && result.Message == "Không tìm thấy trận đấu.") return NotFound();
        return FromResult(result);
    }

    // PUT /api/matches/{id}/pick-ban — { steps: [{ action, side, gameMapId }] }
    [HttpPut]
    public async Task<IActionResult> Save(int matchId, [FromBody] SaveMatchPickBanRequest request)
    {
        if (!_currentUser.IsAuthenticated) return Unauthorized();

        var accountId = _currentUser.UserId!.Value;
        var allowed = await _pickBanService.CanRecordAsync(matchId, accountId);
        if (allowed is null) return NotFound();
        if (allowed == false) return StatusCode(403);

        return FromResult(await _pickBanService.SaveAsync(matchId, accountId, request));
    }
}
