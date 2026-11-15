using ETOMS_API.DTOs.Profile;
using ETOMS_API.Services.Interface;
using Microsoft.AspNetCore.Mvc;

namespace ETOMS_API.Controllers;

[Route("api/profiles")]
public sealed class ProfilesController : ApiControllerBase
{
    private readonly IProfileService _profileService;

    public ProfilesController(IProfileService profileService)
    {
        _profileService = profileService;
    }

    [HttpGet("{accountId:int}")]
    public async Task<IActionResult> GetProfile(int accountId, CancellationToken cancellationToken)
        => FromResult(await _profileService.GetProfileAsync(accountId, cancellationToken));

    [HttpPut("{accountId:int}")]
    public async Task<IActionResult> UpdateProfile(
        int accountId,
        [FromBody] UpdateProfileRequest request,
        CancellationToken cancellationToken)
        => FromResult(await _profileService.UpdateProfileAsync(accountId, request, cancellationToken));

    [HttpPut("{accountId:int}/games/{gameId:int}")]
    public async Task<IActionResult> UpsertGameAccount(
        int accountId,
        int gameId,
        [FromBody] UpsertGameAccountRequest request,
        CancellationToken cancellationToken)
        => FromResult(await _profileService.UpsertGameAccountAsync(
            accountId, gameId, request.InGameName, cancellationToken));

    [HttpPost("{accountId:int}/identity-documents")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadIdentityDocument(
        int accountId,
        [FromForm] IFormFile file,
        CancellationToken cancellationToken)
        => FromResult(await _profileService.UploadIdentityDocumentAsync(accountId, file, cancellationToken));

    [HttpGet("{accountId:int}/identity-documents")]
    public async Task<IActionResult> GetIdentityDocuments(int accountId, CancellationToken cancellationToken)
        => FromResult(await _profileService.GetIdentityDocumentsAsync(accountId, cancellationToken));

    [HttpGet("{accountId:int}/public")]
    public async Task<IActionResult> GetPublicProfile(int accountId, CancellationToken cancellationToken)
        => FromResult(await _profileService.GetPublicProfileAsync(accountId, cancellationToken));

    [HttpGet("{accountId:int}/match-history")]
    public async Task<IActionResult> GetMatchHistory(int accountId, CancellationToken cancellationToken)
        => FromResult(await _profileService.GetMatchHistoryAsync(accountId, cancellationToken));

    [HttpGet("{accountId:int}/notification-preferences")]
    public async Task<IActionResult> GetNotificationPreferences(int accountId, CancellationToken cancellationToken)
        => FromResult(await _profileService.GetNotificationPreferencesAsync(accountId, cancellationToken));

    [HttpPut("{accountId:int}/notification-preferences")]
    public async Task<IActionResult> UpdateNotificationPreferences(
        int accountId,
        [FromBody] UpdateNotificationPreferencesRequest request,
        CancellationToken cancellationToken)
        => FromResult(await _profileService.UpdateNotificationPreferencesAsync(accountId, request, cancellationToken));
}
