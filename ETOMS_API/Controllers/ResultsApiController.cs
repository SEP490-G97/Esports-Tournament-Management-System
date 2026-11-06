using ETOMS_API.DTOs.Result;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using Microsoft.AspNetCore.Mvc;

namespace ETOMS_API.Controllers;

// API JSON cho module RES (RES-01, 04, 05, 06, 07, 08, 09) — song song với ResultController (MVC).
// RES-02 (UC-28) chạy tự động trong Service, không có endpoint riêng. RES-03 là của Vũ Đức Hải.
[Route("api/matches/{matchId:int}/result")]
public class ResultsApiController : ApiControllerBase
{
    private readonly IResultService _resultService;
    private readonly ICurrentUser _currentUser;
    private readonly IWebHostEnvironment _env;

    public ResultsApiController(IResultService resultService, ICurrentUser currentUser, IWebHostEnvironment env)
    {
        _resultService = resultService;
        _currentUser = currentUser;
        _env = env;
    }

    // GET /api/matches/{matchId}/result/submission — RES-01, trang nộp báo cáo của chính tôi.
    [HttpGet("submission")]
    public async Task<IActionResult> GetSubmissionPage(int matchId)
    {
        if (!_currentUser.IsAuthenticated) return Unauthorized();
        var page = await _resultService.GetSubmissionPageAsync(matchId, _currentUser.UserId!.Value);
        return page is null ? NotFound() : Ok(page);
    }

    // POST /api/matches/{matchId}/result/submission — RES-01 (multipart/form-data: MyScore, OpponentScore, evidenceFiles).
    [HttpPost("submission")]
    public async Task<IActionResult> Submit(int matchId, [FromForm] SubmitReportRequest model, List<IFormFile>? evidenceFiles)
    {
        if (!_currentUser.IsAuthenticated) return Unauthorized();
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var saved = new List<(string Url, string FileType, long? SizeBytes)>();
        if (evidenceFiles is not null)
        {
            foreach (var f in evidenceFiles.Where(f => f.Length > 0))
            {
                var up = EvidenceUploader.Save(f, _env, "match-evidence");
                if (!up.Ok) return BadRequest(new { message = up.Error });
                saved.Add((up.Url!, up.FileType!, up.SizeBytes));
            }
        }

        return FromResult(await _resultService.SubmitReportAsync(matchId, _currentUser.UserId!.Value, model, saved));
    }

    // GET /api/matches/{matchId}/result/evidence — RES-04, công khai (ẩn tới khi đủ 2 báo cáo — BR-RES-02).
    [HttpGet("evidence")]
    public async Task<IActionResult> GetEvidence(int matchId)
    {
        var dto = await _resultService.GetEvidenceAsync(matchId, _currentUser.UserId);
        return dto is null ? NotFound() : Ok(dto);
    }

    // GET /api/matches/{matchId}/result/history — RES-09, công khai.
    [HttpGet("history")]
    public async Task<IActionResult> GetHistory(int matchId)
    {
        var dto = await _resultService.GetAuditTrailAsync(matchId);
        return dto is null ? NotFound() : Ok(dto);
    }

    // POST /api/matches/{matchId}/result/flag — RES-05.
    [HttpPost("flag")]
    public async Task<IActionResult> Flag(int matchId, [FromBody] FlagMatchRequest model)
    {
        if (!_currentUser.IsAuthenticated) return Unauthorized();
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        return FromResult(await _resultService.FlagMatchAsync(matchId, _currentUser.UserId!.Value, model));
    }
}

// Hàng đợi/chi tiết tranh chấp và phán quyết VAR gắn với DisputeID, không gắn với 1 trận cụ thể
// trên URL (1 trận chỉ mở được 1 tranh chấp tại 1 thời điểm — BR-RES-07) nên tách route riêng.
[Route("api/disputes")]
public class DisputesApiController : ApiControllerBase
{
    private readonly IResultService _resultService;
    private readonly ICurrentUser _currentUser;

    public DisputesApiController(IResultService resultService, ICurrentUser currentUser)
    {
        _resultService = resultService;
        _currentUser = currentUser;
    }

    // GET /api/disputes — RES-06, hàng đợi của Trọng tài được gán / Ban tổ chức.
    [HttpGet]
    public async Task<IActionResult> GetQueue()
    {
        if (!_currentUser.IsAuthenticated) return Unauthorized();
        return Ok(await _resultService.GetDisputeQueueAsync(_currentUser.UserId!.Value));
    }

    // GET /api/disputes/{id} — RES-06 chi tiết.
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetDetail(int id)
    {
        if (!_currentUser.IsAuthenticated) return Unauthorized();
        var isAdjudicator = await _resultService.IsAdjudicatorAsync(id, _currentUser.UserId!.Value);
        if (isAdjudicator is null) return NotFound();
        if (isAdjudicator != true) return StatusCode(403);
        var dto = await _resultService.GetDisputeDetailAsync(id, _currentUser.UserId!.Value);
        return dto is null ? NotFound() : Ok(dto);
    }

    // POST /api/disputes/{id}/force-update — RES-07.
    [HttpPost("{id:int}/force-update")]
    public async Task<IActionResult> ForceUpdate(int id, [FromBody] ForceUpdateRequest model)
    {
        if (!_currentUser.IsAuthenticated) return Unauthorized();
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        return FromResult(await _resultService.ForceUpdateAsync(id, _currentUser.UserId!.Value, model));
    }

    // POST /api/disputes/{id}/appeal — RES-08, đội trưởng khiếu nại.
    [HttpPost("{id:int}/appeal")]
    public async Task<IActionResult> Appeal(int id, [FromBody] AppealRequest model)
    {
        if (!_currentUser.IsAuthenticated) return Unauthorized();
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        return FromResult(await _resultService.AppealAsync(id, _currentUser.UserId!.Value, model));
    }

    // POST /api/disputes/{id}/appeal/decision — RES-08, Trọng tài/BTC kết luận khiếu nại.
    [HttpPost("{id:int}/appeal/decision")]
    public async Task<IActionResult> DecideAppeal(int id, [FromBody] AppealDecisionRequest model)
    {
        if (!_currentUser.IsAuthenticated) return Unauthorized();
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        return FromResult(await _resultService.DecideAppealAsync(id, _currentUser.UserId!.Value, model));
    }
}
