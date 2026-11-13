using ETOMS_API.DTOs.Profile;
using ETOMS_API.Utils;

namespace ETOMS_API.Services.Interface;

public interface IProfileService
{
    Task<Result<ProfileResponse>> GetProfileAsync(int accountId, CancellationToken cancellationToken);
    Task<Result<ProfileResponse>> UpdateProfileAsync(int accountId, UpdateProfileRequest request, CancellationToken cancellationToken);
    Task<Result<CredentialResponse>> UpsertGameAccountAsync(int accountId, int gameId, string inGameName, CancellationToken cancellationToken);
    Task<Result<CredentialResponse>> UploadIdentityDocumentAsync(int accountId, IFormFile file, CancellationToken cancellationToken);
    Task<Result<IReadOnlyList<CredentialResponse>>> GetIdentityDocumentsAsync(int accountId, CancellationToken cancellationToken);
    Task<Result<IdentityDocumentDownload>> GetIdentityDocumentDownloadAsync(int accountId, int credentialId, int requesterAccountId, CancellationToken cancellationToken);
    Task<Result<PublicProfileResponse>> GetPublicProfileAsync(int accountId, CancellationToken cancellationToken);
    Task<Result<IReadOnlyList<MatchHistoryResponse>>> GetMatchHistoryAsync(int accountId, CancellationToken cancellationToken);
    Task<Result<NotificationPreferencesResponse>> GetNotificationPreferencesAsync(int accountId, CancellationToken cancellationToken);
    Task<Result<NotificationPreferencesResponse>> UpdateNotificationPreferencesAsync(int accountId, UpdateNotificationPreferencesRequest request, CancellationToken cancellationToken);
}
