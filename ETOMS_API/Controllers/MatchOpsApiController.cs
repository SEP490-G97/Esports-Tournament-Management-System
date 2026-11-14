using ETOMS_API.DTOs.Match;
using ETOMS_API.Services.Interface;
using Microsoft.AspNetCore.Mvc;

namespace ETOMS_API.Controllers;

// STUB TẠM thay module MAT (Vũ Đức Hải: MAT-01/04/05/06/07, Nguyễn Quang Anh: MAT-08..12) —
// chỉ đủ để demo luồng vào trận, không phải bài làm cá nhân của hai bạn.
[Route("api/matches/{matchId:int}/ops")]
public class MatchOpsApiController : ApiControllerBase
{
    private readonly IMatchOpsService _ops;
    private readonly ICurrentUser _currentUser;

    public MatchOpsApiController(IMatchOpsService ops, ICurrentUser currentUser)
    {
        _ops = ops;
        _currentUser = currentUser;
    }

    // GET /api/matches/{matchId}/ops — MAT-01, công khai (Guest xem được thông tin cơ bản,
    // RoomCode/Password chỉ hiện cho 2 đội trưởng/trọng tài/BTC).
    [HttpGet]
    public async Task<IActionResult> GetDetail(int matchId)
    {
        var detail = await _ops.GetMatchDetailAsync(matchId, _currentUser.IsAuthenticated ? _currentUser.UserId : null);
        return detail is null ? NotFound() : Ok(detail);
    }

    // POST /api/matches/{matchId}/ops/checkin — MAT-04.
    [HttpPost("checkin")]
    public async Task<IActionResult> ManualCheckIn(int matchId, [FromBody] ManualCheckInRequest model)
    {
        if (!_currentUser.IsAuthenticated) return Unauthorized();
        return FromResult(await _ops.ManualCheckInAsync(matchId, _currentUser.UserId!.Value, model));
    }

    // PUT /api/matches/{matchId}/ops/room — MAT-08.
    [HttpPut("room")]
    public async Task<IActionResult> SetRoomInfo(int matchId, [FromBody] SetRoomInfoRequest model)
    {
        if (!_currentUser.IsAuthenticated) return Unauthorized();
        return FromResult(await _ops.SetRoomInfoAsync(matchId, _currentUser.UserId!.Value, model));
    }

    // POST /api/matches/{matchId}/ops/substitute-requests — MAT-05.
    [HttpPost("substitute-requests")]
    public async Task<IActionResult> RequestSubstitute(int matchId, [FromBody] CreateSubstituteRequest model)
    {
        if (!_currentUser.IsAuthenticated) return Unauthorized();
        return FromResult(await _ops.RequestSubstituteAsync(matchId, _currentUser.UserId!.Value, model));
    }

    // GET /api/matches/{matchId}/ops/substitute-requests — danh sách, cho trọng tài/BTC/2 đội xem.
    [HttpGet("substitute-requests")]
    public async Task<IActionResult> GetSubstituteRequests(int matchId)
    {
        if (!_currentUser.IsAuthenticated) return Unauthorized();
        return FromResult(await _ops.GetSubstituteRequestsAsync(matchId, _currentUser.UserId!.Value));
    }

    // POST /api/matches/{matchId}/ops/substitute-requests/{requestId}/decide — MAT-06.
    [HttpPost("substitute-requests/{requestId:int}/decide")]
    public async Task<IActionResult> DecideSubstitute(int matchId, int requestId, [FromBody] DecideSubstituteRequest model)
    {
        if (!_currentUser.IsAuthenticated) return Unauthorized();
        return FromResult(await _ops.DecideSubstituteAsync(requestId, _currentUser.UserId!.Value, model.Approve));
    }

    // GET /api/matches/{matchId}/ops/chat — MAT-07.
    [HttpGet("chat")]
    public async Task<IActionResult> GetChat(int matchId)
    {
        if (!_currentUser.IsAuthenticated) return Unauthorized();
        return FromResult(await _ops.GetChatMessagesAsync(matchId, _currentUser.UserId!.Value));
    }

    // POST /api/matches/{matchId}/ops/chat — MAT-07.
    [HttpPost("chat")]
    public async Task<IActionResult> SendChat(int matchId, [FromBody] SendChatMessageRequest model)
    {
        if (!_currentUser.IsAuthenticated) return Unauthorized();
        return FromResult(await _ops.SendChatMessageAsync(matchId, _currentUser.UserId!.Value, model));
    }

    // POST /api/matches/{matchId}/ops/walkover — MAT-09.
    [HttpPost("walkover")]
    public async Task<IActionResult> Walkover(int matchId, [FromBody] WalkoverRequest model)
    {
        if (!_currentUser.IsAuthenticated) return Unauthorized();
        return FromResult(await _ops.ProcessWalkoverAsync(matchId, _currentUser.UserId!.Value, model));
    }

    // POST /api/matches/{matchId}/ops/rematch — MAT-10.
    [HttpPost("rematch")]
    public async Task<IActionResult> Rematch(int matchId, [FromBody] RematchRequest model)
    {
        if (!_currentUser.IsAuthenticated) return Unauthorized();
        return FromResult(await _ops.RematchAsync(matchId, _currentUser.UserId!.Value, model));
    }

    // POST /api/matches/{matchId}/ops/disqualify — MAT-11.
    [HttpPost("disqualify")]
    public async Task<IActionResult> Disqualify(int matchId, [FromBody] DisqualifyRequest model)
    {
        if (!_currentUser.IsAuthenticated) return Unauthorized();
        return FromResult(await _ops.DisqualifyAsync(matchId, _currentUser.UserId!.Value, model));
    }

    // POST /api/matches/{matchId}/ops/forfeit — MAT-12.
    [HttpPost("forfeit")]
    public async Task<IActionResult> Forfeit(int matchId, [FromBody] ForfeitRequest model)
    {
        if (!_currentUser.IsAuthenticated) return Unauthorized();
        return FromResult(await _ops.ForfeitAsync(matchId, _currentUser.UserId!.Value, model));
    }
}

public class DecideSubstituteRequest { public bool Approve { get; set; } }
