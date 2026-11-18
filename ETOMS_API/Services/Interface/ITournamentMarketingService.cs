using ETOMS_API.DTOs.Marketing;
using ETOMS_API.Utils;

namespace ETOMS_API.Services.Interface;

public interface ITournamentMarketingService
{
    Task<Result<TournamentLandingResponse>> GetLandingAsync(int tournamentId, CancellationToken cancellationToken);
    Task<Result<IReadOnlyList<MarketingTournamentResponse>>> GetOrganizerTournamentsAsync(int accountId, CancellationToken cancellationToken);
    Task<Result<IReadOnlyList<PromotionLinkResponse>>> GetPromotionLinksAsync(int tournamentId, int accountId, Uri publicBaseUri, CancellationToken cancellationToken);
    Task<Result<PromotionLinkResponse>> CreatePromotionLinkAsync(int tournamentId, int accountId, CreatePromotionLinkRequest request, Uri publicBaseUri, CancellationToken cancellationToken);
    Task<Result> TrackVisitAsync(string code, CancellationToken cancellationToken);
    Task<Result<string>> GetRegistrationUrlAsync(int tournamentId, string? code, Uri publicBaseUri, CancellationToken cancellationToken);
}
