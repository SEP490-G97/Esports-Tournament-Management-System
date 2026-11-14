using ETOMS_API.DTOs.Profile;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using Microsoft.AspNetCore.Mvc;

namespace ETOMS_API.Controllers;

[Route("api/profiles")]
public sealed class ProfilesController : ApiControllerBase
{
    private readonly IProfileService _profileService;
    private readonly ICurrentUser _currentUser;
    private readonly PrivateFileStorage _fileStorage;

    public ProfilesController(
        IProfileService profileService,
        ICurrentUser currentUser,
        PrivateFileStorage fileStorage)
    {
        _profileService = profileService;
        _currentUser = currentUser;
        _fileStorage = fileStorage;
    }

    [HttpGet("{accountId:int}")]
    public async Task<IActionResult> GetProfile(int accountId, CancellationToken cancellationToken)
    {
        var accessDenied = RequireOwner(accountId);
        return accessDenied ?? FromResult(await _profileService.GetProfileAsync(accountId, cancellationToken));
    }

    [HttpPut("{accountId:int}")]
    public async Task<IActionResult> UpdateProfile(
        int accountId,
        [FromBody] UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        var accessDenied = RequireOwner(accountId);
        return accessDenied ?? FromResult(await _profileService.UpdateProfileAsync(accountId, request, cancellationToken));
    }

    // PRF-01: ảnh đại diện chọn từ máy (multipart, trường "file").
    [HttpPost("{accountId:int}/avatar")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(3 * 1024 * 1024)]
    public async Task<IActionResult> UploadAvatar(int accountId, IFormFile? file, CancellationToken cancellationToken)
    {
        var accessDenied = RequireOwner(accountId);
        return accessDenied ?? FromResult(await _profileService.UploadAvatarAsync(accountId, file, cancellationToken));
    }

    [HttpPut("{accountId:int}/games/{gameId:int}")]
    public async Task<IActionResult> UpsertGameAccount(
        int accountId,
        int gameId,
        [FromBody] UpsertGameAccountRequest request,
        CancellationToken cancellationToken)
    {
        var accessDenied = RequireOwner(accountId);
        return accessDenied ?? FromResult(await _profileService.UpsertGameAccountAsync(
            accountId, gameId, request.InGameName, cancellationToken));
    }

    [HttpPost("{accountId:int}/identity-documents")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadIdentityDocument(
        int accountId,
        [FromForm] IFormFile file,
        CancellationToken cancellationToken)
    {
        var accessDenied = RequireOwner(accountId);
        return accessDenied ?? FromResult(await _profileService.UploadIdentityDocumentAsync(accountId, file, cancellationToken));
    }

    [HttpGet("{accountId:int}/identity-documents")]
    public async Task<IActionResult> GetIdentityDocuments(int accountId, CancellationToken cancellationToken)
    {
        var accessDenied = RequireOwner(accountId);
        return accessDenied ?? FromResult(await _profileService.GetIdentityDocumentsAsync(accountId, cancellationToken));
    }

    [HttpGet("{accountId:int}/identity-documents/{credentialId:int}/file")]
    public async Task<IActionResult> DownloadIdentityDocument(
        int accountId,
        int credentialId,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || !_currentUser.UserId.HasValue)
        {
            return Unauthorized();
        }

        var result = await _profileService.GetIdentityDocumentDownloadAsync(
            accountId, credentialId, _currentUser.UserId.Value, cancellationToken);
        if (!result.IsSuccess)
        {
            return FromResult(result);
        }

        var file = result.Data!;
        return PrivateFile(_fileStorage, file.RelativePath, file.FileName);
    }

    [HttpGet("{accountId:int}/public")]
    public async Task<IActionResult> GetPublicProfile(int accountId, CancellationToken cancellationToken)
        => FromResult(await _profileService.GetPublicProfileAsync(accountId, cancellationToken));

    [HttpGet("{accountId:int}/match-history")]
    public async Task<IActionResult> GetMatchHistory(int accountId, CancellationToken cancellationToken)
        => FromResult(await _profileService.GetMatchHistoryAsync(accountId, cancellationToken));

    [HttpGet("{accountId:int}/notification-preferences")]
    public async Task<IActionResult> GetNotificationPreferences(int accountId, CancellationToken cancellationToken)
    {
        var accessDenied = RequireOwner(accountId);
        return accessDenied ?? FromResult(await _profileService.GetNotificationPreferencesAsync(accountId, cancellationToken));
    }

    [HttpPut("{accountId:int}/notification-preferences")]
    public async Task<IActionResult> UpdateNotificationPreferences(
        int accountId,
        [FromBody] UpdateNotificationPreferencesRequest request,
        CancellationToken cancellationToken)
    {
        var accessDenied = RequireOwner(accountId);
        return accessDenied ?? FromResult(await _profileService.UpdateNotificationPreferencesAsync(accountId, request, cancellationToken));
    }

    private IActionResult? RequireOwner(int accountId)
    {
        if (!_currentUser.IsAuthenticated || !_currentUser.UserId.HasValue)
        {
            return Unauthorized();
        }

        return _currentUser.UserId.Value == accountId ? null : Forbid();
    }
}
