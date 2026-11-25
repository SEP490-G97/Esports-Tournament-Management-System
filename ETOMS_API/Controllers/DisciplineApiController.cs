using ETOMS_API.DTOs.Discipline;
using ETOMS_API.Services.Interface;
using Microsoft.AspNetCore.Mvc;

namespace ETOMS_API.Controllers;

// Module PEN — UC-34 (cấp / hủy thẻ phạt), UC-35 (ngưỡng loại tự động). Quyền kiểm trong DisciplineService:
// BTC của giải, hoặc trọng tài được phân công trận của đội (theo tiền điều kiện UC-34).
[Route("api/tournaments/{tournamentId:int}")]
public class DisciplineApiController : ApiControllerBase
{
    private readonly IDisciplineService _discipline;
    private readonly ICurrentUser _currentUser;

    public DisciplineApiController(IDisciplineService discipline, ICurrentUser currentUser)
    {
        _discipline = discipline;
        _currentUser = currentUser;
    }

    // GET /api/tournaments/{id}/discipline — tổng điểm phạt từng đội, ngưỡng, danh sách thẻ (kể cả dòng hủy).
    [HttpGet("discipline")]
    public async Task<IActionResult> Board(int tournamentId, CancellationToken ct)
    {
        if (!_currentUser.IsAuthenticated) return Unauthorized();
        return FromResult(await _discipline.GetBoardAsync(tournamentId, _currentUser.UserId!.Value, ct));
    }

    // POST /api/tournaments/{id}/penalties — UC-34 bước 1–6.
    [HttpPost("penalties")]
    public async Task<IActionResult> Issue(int tournamentId, [FromBody] IssuePenaltyRequest model, CancellationToken ct)
    {
        if (!_currentUser.IsAuthenticated) return Unauthorized();
        return FromResult(await _discipline.IssueAsync(tournamentId, _currentUser.UserId!.Value, model, ct));
    }

    // POST /api/tournaments/{id}/penalties/{penaltyId}/cancel — UC-34 A2 (không xóa: ghi dòng hủy liên kết).
    [HttpPost("penalties/{penaltyId:int}/cancel")]
    public async Task<IActionResult> Cancel(int tournamentId, int penaltyId, [FromBody] CancelPenaltyRequest model, CancellationToken ct)
    {
        if (!_currentUser.IsAuthenticated) return Unauthorized();
        return FromResult(await _discipline.CancelAsync(tournamentId, penaltyId, _currentUser.UserId!.Value, model, ct));
    }

    // PUT /api/tournaments/{id}/penalty-threshold — ngưỡng của UC-35, đặt trước khi giải bắt đầu.
    [HttpPut("penalty-threshold")]
    public async Task<IActionResult> SetThreshold(int tournamentId, [FromBody] SetPenaltyThresholdRequest model, CancellationToken ct)
    {
        if (!_currentUser.IsAuthenticated) return Unauthorized();
        return FromResult(await _discipline.SetThresholdAsync(tournamentId, _currentUser.UserId!.Value, model, ct));
    }

    // GET /api/teams/{teamId}/penalties — hồ sơ kỷ luật công khai của đội (UC-34 hậu điều kiện, BR-PEN-01).
    [HttpGet("~/api/teams/{teamId:int}/penalties")]
    public async Task<IActionResult> TeamRecord(int teamId, CancellationToken ct)
        => Ok(await _discipline.GetTeamRecordAsync(teamId, ct));
}
