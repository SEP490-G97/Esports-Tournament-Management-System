using ETOMS_API.DTOs.Registration;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace ETOMS_API.Controllers;

[Route("api")]
public sealed class TournamentRegistrationsController : ApiControllerBase
{
    private static readonly JsonSerializerOptions WebJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ITournamentRegistrationService _registrationService;
    private readonly ICurrentUser _currentUser;
    private readonly PrivateFileStorage _fileStorage;

    public TournamentRegistrationsController(
        ITournamentRegistrationService registrationService,
        ICurrentUser currentUser,
        PrivateFileStorage fileStorage)
    {
        _registrationService = registrationService;
        _currentUser = currentUser;
        _fileStorage = fileStorage;
    }

    [HttpGet("tournaments/{tournamentId:int}/registration-options")]
    public async Task<IActionResult> GetOptions(int tournamentId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var accountId)) return Unauthorized();
        return FromResult(await _registrationService.GetOptionsAsync(tournamentId, accountId, cancellationToken));
    }

    [HttpPost("tournaments/{tournamentId:int}/registrations")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Create(
        int tournamentId,
        [FromForm] int teamId,
        [FromForm] string lineupJson,
        [FromForm] bool saveAsDraft,
        [FromForm] bool acceptRulebook,
        [FromForm] string? promotionLinkCode,
        [FromForm] decimal? declaredAmount,
        [FromForm] IFormFile? paymentProof,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var accountId)) return Unauthorized();
        IReadOnlyList<RegistrationLineupRequest>? lineup;
        try
        {
            lineup = JsonSerializer.Deserialize<IReadOnlyList<RegistrationLineupRequest>>(
                lineupJson,
                WebJsonOptions);
        }
        catch (JsonException)
        {
            return BadRequest(new { message = "Đội hình gửi lên không đúng định dạng JSON." });
        }
        if (lineup is null)
        {
            return BadRequest(new { message = "Cần chọn đội hình đăng ký." });
        }
        var result = await _registrationService.CreateAsync(
            tournamentId,
            accountId,
            new CreateRegistrationRequest
            {
                TeamId = teamId,
                Lineup = lineup,
                SaveAsDraft = saveAsDraft,
                AcceptRulebook = acceptRulebook,
                PromotionLinkCode = promotionLinkCode
            },
            paymentProof,
            declaredAmount,
            cancellationToken);
        if (!result.IsSuccess) return FromResult(result);
        return CreatedAtAction(nameof(GetById), new { registrationId = result.Data!.RegistrationId }, new
        {
            message = result.Message,
            data = result.Data
        });
    }

    [HttpGet("registrations/mine")]
    public async Task<IActionResult> GetMine(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var accountId)) return Unauthorized();
        return FromResult(await _registrationService.GetMineAsync(accountId, cancellationToken));
    }

    [HttpGet("registrations/{registrationId:int}")]
    public async Task<IActionResult> GetById(int registrationId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var accountId)) return Unauthorized();
        return FromResult(await _registrationService.GetAsync(registrationId, accountId, cancellationToken));
    }

    [HttpPost("registrations/{registrationId:int}/withdraw")]
    public async Task<IActionResult> Withdraw(int registrationId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var accountId)) return Unauthorized();
        return FromResult(await _registrationService.WithdrawAsync(registrationId, accountId, cancellationToken));
    }

    [HttpPost("registrations/{registrationId:int}/waitlist-confirmation")]
    public async Task<IActionResult> AcceptWaitlistOffer(int registrationId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var accountId)) return Unauthorized();
        return FromResult(await _registrationService.AcceptWaitlistOfferAsync(
            registrationId, accountId, cancellationToken));
    }

    [HttpPost("registrations/{registrationId:int}/payment-proof")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadPaymentProof(
        int registrationId,
        [FromForm] IFormFile file,
        [FromForm] decimal? declaredAmount,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var accountId)) return Unauthorized();
        return FromResult(await _registrationService.UploadPaymentProofAsync(
            registrationId, accountId, file, declaredAmount, cancellationToken));
    }

    [HttpGet("tournaments/{tournamentId:int}/registrations")]
    public async Task<IActionResult> GetTournamentRegistrations(
        int tournamentId,
        [FromQuery] string? status,
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var accountId)) return Unauthorized();
        return FromResult(await _registrationService.GetTournamentRegistrationsAsync(
            tournamentId, accountId, status, search, cancellationToken));
    }

    [HttpGet("tournaments/{tournamentId:int}/registrations/{registrationId:int}")]
    public async Task<IActionResult> GetReview(
        int tournamentId,
        int registrationId,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var accountId)) return Unauthorized();
        return FromResult(await _registrationService.GetReviewAsync(
            tournamentId, registrationId, accountId, cancellationToken));
    }

    [HttpPost("tournaments/{tournamentId:int}/registrations/{registrationId:int}/review")]
    public async Task<IActionResult> Review(
        int tournamentId,
        int registrationId,
        [FromBody] ReviewRegistrationRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var accountId)) return Unauthorized();
        return FromResult(await _registrationService.ReviewAsync(
            tournamentId, registrationId, accountId, request, cancellationToken));
    }

    [HttpPut("tournaments/{tournamentId:int}/registrations/{registrationId:int}/payment-status")]
    public async Task<IActionResult> VerifyPayment(
        int tournamentId,
        int registrationId,
        [FromBody] VerifyRegistrationPaymentRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var accountId)) return Unauthorized();
        return FromResult(await _registrationService.VerifyPaymentAsync(
            tournamentId, registrationId, accountId, request, cancellationToken));
    }

    [HttpPost("tournaments/{tournamentId:int}/registrations/{registrationId:int}/refund")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> RecordRefund(
        int tournamentId,
        int registrationId,
        [FromForm] decimal? amount,
        [FromForm] DateOnly? refundDate,
        [FromForm] string refundMethod,
        [FromForm] string? note,
        [FromForm] IFormFile? evidence,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var accountId)) return Unauthorized();
        return FromResult(await _registrationService.RecordRefundAsync(
            tournamentId,
            registrationId,
            accountId,
            new RefundRegistrationRequest
            {
                Amount = amount,
                RefundDate = refundDate,
                RefundMethod = refundMethod,
                Note = note
            },
            evidence,
            cancellationToken));
    }

    [HttpPost("tournaments/{tournamentId:int}/waitlist/promote")]
    public async Task<IActionResult> PromoteWaitlist(
        int tournamentId,
        [FromBody] PromoteWaitlistRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var accountId)) return Unauthorized();
        return FromResult(await _registrationService.PromoteWaitlistAsync(
            tournamentId, request.RegistrationId, accountId, cancellationToken));
    }

    [HttpPut("tournaments/{tournamentId:int}/tie-breakers")]
    public async Task<IActionResult> SetTieBreakerOrder(
        int tournamentId,
        [FromBody] SetTieBreakerOrderRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var accountId)) return Unauthorized();
        return FromResult(await _registrationService.SetTieBreakerOrderAsync(
            tournamentId, accountId, request.Order, cancellationToken));
    }

    [HttpGet("registrations/{registrationId:int}/players/{playerAccountId:int}/identity-documents/{credentialId:int}")]
    public async Task<IActionResult> GetIdentityDocument(
        int registrationId,
        int playerAccountId,
        int credentialId,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var accountId)) return Unauthorized();
        var result = await _registrationService.GetIdentityDocumentAsync(
            registrationId, playerAccountId, credentialId, accountId, cancellationToken);
        if (!result.IsSuccess) return FromResult(result);
        var file = result.Data!;
        return PrivateFile(_fileStorage, file.RelativePath, file.FileName);
    }

    [HttpGet("registrations/{registrationId:int}/payments/{paymentRecordId:int}/file")]
    public async Task<IActionResult> GetPaymentRecordFile(
        int registrationId,
        int paymentRecordId,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var accountId)) return Unauthorized();
        var result = await _registrationService.GetPaymentRecordFileAsync(
            registrationId, paymentRecordId, accountId, cancellationToken);
        if (!result.IsSuccess) return FromResult(result);
        var file = result.Data!;
        return PrivateFile(_fileStorage, file.RelativePath, file.FileName);
    }

    private bool TryGetUserId(out int accountId)
    {
        accountId = _currentUser.UserId.GetValueOrDefault();
        return _currentUser.IsAuthenticated && accountId > 0;
    }
}

public sealed record PromoteWaitlistRequest
{
    public int? RegistrationId { get; init; }
}
