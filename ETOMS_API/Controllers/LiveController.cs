using ETOMS_API.DTOs.Live;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using Microsoft.AspNetCore.Mvc;

namespace ETOMS_API.Controllers;

// UC-45 tỉ số trực tiếp (LIVE-01/02/04) và UC-46 livestream.
// Xem: ai cũng xem được (Guest). Ghi: phải đăng nhập; quyền cụ thể (trọng tài/BTC) kiểm ở service.
[Route("api")]
public class LiveController : ApiControllerBase
{
    private readonly ILiveService _live;
    private readonly ICurrentUser _currentUser;

    public LiveController(ILiveService live, ICurrentUser currentUser)
    {
        _live = live;
        _currentUser = currentUser;
    }

    private int? Viewer => _currentUser.IsAuthenticated ? _currentUser.UserId : null;

    private IActionResult NeedLogin()
        => StatusCode(StatusCodes.Status401Unauthorized, new { message = "Vui lòng đăng nhập để thực hiện thao tác này.", fieldErrors = new Dictionary<string, string[]>() });

    // GET /api/matches/{matchId}/live — tỉ số từng ván + livestream gán vào trận (LIVE-02, UC-46).
    [HttpGet("matches/{matchId:int}/live")]
    public async Task<IActionResult> GetMatchLive(int matchId)
        => FromResult(await _live.GetMatchLiveAsync(matchId, Viewer));

    // PUT /api/matches/{matchId}/live/games/{gameNo} — LIVE-01 nhập/sửa tỉ số một ván.
    [HttpPut("matches/{matchId:int}/live/games/{gameNo:int}")]
    public async Task<IActionResult> SaveGame(int matchId, int gameNo, [FromBody] SaveLiveGameRequest model)
    {
        if (Viewer is not int me) return NeedLogin();
        return FromResult(await _live.SaveGameAsync(matchId, gameNo, me, model));
    }

    // DELETE /api/matches/{matchId}/live/games/{gameNo} — xóa ván cuối khi nhập nhầm.
    [HttpDelete("matches/{matchId:int}/live/games/{gameNo:int}")]
    public async Task<IActionResult> ClearGame(int matchId, int gameNo)
    {
        if (Viewer is not int me) return NeedLogin();
        return FromResult(await _live.ClearGameAsync(matchId, gameNo, me));
    }

    // GET /api/tournaments/{tournamentId}/livestreams — tab "Trực tiếp" của giải.
    [HttpGet("tournaments/{tournamentId:int}/livestreams")]
    public async Task<IActionResult> GetStreams(int tournamentId)
        => FromResult(await _live.GetTournamentStreamsAsync(tournamentId, Viewer));

    [HttpPost("tournaments/{tournamentId:int}/livestreams")]
    public async Task<IActionResult> CreateStream(int tournamentId, [FromBody] SaveLivestreamRequest model)
    {
        if (Viewer is not int me) return NeedLogin();
        return FromResult(await _live.CreateStreamAsync(tournamentId, me, model));
    }

    [HttpPut("tournaments/{tournamentId:int}/livestreams/{livestreamId:int}")]
    public async Task<IActionResult> UpdateStream(int tournamentId, int livestreamId, [FromBody] SaveLivestreamRequest model)
    {
        if (Viewer is not int me) return NeedLogin();
        return FromResult(await _live.UpdateStreamAsync(tournamentId, livestreamId, me, model));
    }

    [HttpDelete("tournaments/{tournamentId:int}/livestreams/{livestreamId:int}")]
    public async Task<IActionResult> DeleteStream(int tournamentId, int livestreamId)
    {
        if (Viewer is not int me) return NeedLogin();
        return FromResult(await _live.DeleteStreamAsync(tournamentId, livestreamId, me));
    }
}
