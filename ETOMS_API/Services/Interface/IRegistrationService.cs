using ETOMS_API.DTOs.Registration;
using ETOMS_API.Utils;

namespace ETOMS_API.Services.Interface;

public interface IRegistrationService
{
    Task<Result<RegistrationOptionsResponse>> GetOptionsAsync(int tournamentId, int accountId, CancellationToken cancellationToken);
    Task<Result<RegistrationResponse>> CreateAsync(int tournamentId, int accountId, CreateRegistrationRequest request, IFormFile? paymentProof, decimal? declaredAmount, CancellationToken cancellationToken);
    Task<Result<RegistrationResponse>> GetAsync(int registrationId, int accountId, CancellationToken cancellationToken);
    Task<Result<IReadOnlyList<RegistrationResponse>>> GetMineAsync(int accountId, CancellationToken cancellationToken);
    Task<Result<RegistrationListResponse>> GetTournamentRegistrationsAsync(int tournamentId, int accountId, string? status, string? search, CancellationToken cancellationToken);
    Task<Result<RegistrationReviewResponse>> GetReviewAsync(int tournamentId, int registrationId, int accountId, CancellationToken cancellationToken);
    Task<Result<PaymentRecordResponse>> UploadPaymentProofAsync(int registrationId, int accountId, IFormFile file, decimal? declaredAmount, CancellationToken cancellationToken);
    Task<Result<RegistrationResponse>> ReviewAsync(int tournamentId, int registrationId, int accountId, ReviewRegistrationRequest request, CancellationToken cancellationToken);
    Task<Result<RegistrationResponse>> VerifyPaymentAsync(int tournamentId, int registrationId, int accountId, VerifyRegistrationPaymentRequest request, CancellationToken cancellationToken);
    Task<Result<RegistrationResponse>> RecordRefundAsync(int tournamentId, int registrationId, int accountId, RefundRegistrationRequest request, IFormFile? evidence, CancellationToken cancellationToken);
    Task<Result<RegistrationResponse>> WithdrawAsync(int registrationId, int accountId, CancellationToken cancellationToken);
    Task<Result<RegistrationResponse>> AcceptWaitlistOfferAsync(int registrationId, int accountId, CancellationToken cancellationToken);
    Task<Result<WaitlistPromotionResponse>> PromoteWaitlistAsync(int tournamentId, int? registrationId, int accountId, CancellationToken cancellationToken);
    Task ProcessWaitlistsAsync(CancellationToken cancellationToken);
    Task<Result<IReadOnlyList<string>>> SetTieBreakerOrderAsync(int tournamentId, int accountId, IReadOnlyList<string> order, CancellationToken cancellationToken);
    Task<Result<PrivateRegistrationFileResponse>> GetIdentityDocumentAsync(int registrationId, int playerAccountId, int credentialId, int requesterAccountId, CancellationToken cancellationToken);
    Task<Result<PrivateRegistrationFileResponse>> GetPaymentRecordFileAsync(int registrationId, int paymentRecordId, int requesterAccountId, CancellationToken cancellationToken);
}
