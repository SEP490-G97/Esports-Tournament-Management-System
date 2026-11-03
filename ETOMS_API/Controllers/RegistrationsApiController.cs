using ETOMS_API.DTOs.Registration;
using ETOMS_API.Services;
using ETOMS_API.Utils;
using Microsoft.AspNetCore.Mvc;

namespace ETOMS_API.Controllers;

// API JSON cho REG-03 (minh chứng), REG-04 (đơn của tôi) — của Dũng — và 2 stub tạm
// REG-01/REG-06/REG-07 (Cấn Việt Hùng chưa code đầy đủ), song song với RegistrationController (MVC).
[Route("api/registrations")]
public class RegistrationsApiController : ApiControllerBase
{
    private readonly IRegistrationService _regService;
    private readonly ICurrentUser _currentUser;
    private readonly IWebHostEnvironment _env;

    public RegistrationsApiController(IRegistrationService regService, ICurrentUser currentUser, IWebHostEnvironment env)
    {
        _regService = regService;
        _currentUser = currentUser;
        _env = env;
    }

    // GET /api/registrations/open-tournaments — danh sách giải đang mở đăng ký.
    [HttpGet("open-tournaments")]
    public async Task<IActionResult> GetOpenTournaments()
        => Ok(await _regService.GetOpenTournamentsAsync());

    // GET /api/registrations/mine — REG-04.
    [HttpGet("mine")]
    public async Task<IActionResult> GetMine()
    {
        if (!_currentUser.IsAuthenticated) return Unauthorized();
        return Ok(await _regService.GetMyRegistrationsAsync(_currentUser.UserId!.Value));
    }

    // POST /api/registrations — STUB TẠM thay REG-01.
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRegistrationRequest model)
    {
        if (!_currentUser.IsAuthenticated) return Unauthorized();
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        return FromResult(await _regService.CreateRegistrationAsync(model, _currentUser.UserId!.Value));
    }

    // POST /api/registrations/{id}/proof — REG-03 (multipart/form-data: proofFile, note).
    [HttpPost("{id:int}/proof")]
    public async Task<IActionResult> UploadProof(int id, IFormFile? proofFile, string? note)
    {
        if (!_currentUser.IsAuthenticated) return Unauthorized();

        var upload = ImageUploader.Save(proofFile, _env, "payment-proofs");
        if (!upload.Ok || upload.Url is null)
            return BadRequest(new { message = upload.Error ?? "Vui lòng chọn ảnh minh chứng." });

        return FromResult(await _regService.UploadPaymentProofAsync(id, _currentUser.UserId!.Value, upload.Url, note));
    }

    // GET /api/registrations/pending?tournamentId= — STUB TẠM thay REG-06/07, chỉ organizer.
    [HttpGet("pending")]
    public async Task<IActionResult> GetPending([FromQuery] int tournamentId)
    {
        if (!_currentUser.IsAuthenticated) return Unauthorized();
        if (!await _regService.IsOrganizerAsync(tournamentId, _currentUser.UserId!.Value)) return StatusCode(403);
        return Ok(await _regService.GetPendingRegistrationsAsync(tournamentId));
    }

    // POST /api/registrations/{id}/approve
    [HttpPost("{id:int}/approve")]
    public async Task<IActionResult> Approve(int id)
    {
        if (!_currentUser.IsAuthenticated) return Unauthorized();
        return FromResult(await _regService.ApproveRegistrationAsync(id, _currentUser.UserId!.Value));
    }

    // POST /api/registrations/{id}/reject
    [HttpPost("{id:int}/reject")]
    public async Task<IActionResult> Reject(int id, [FromBody] RejectRegistrationRequest body)
    {
        if (!_currentUser.IsAuthenticated) return Unauthorized();
        return FromResult(await _regService.RejectRegistrationAsync(id, _currentUser.UserId!.Value, body.Reason));
    }
}

public class RejectRegistrationRequest { public string Reason { get; set; } = string.Empty; }
