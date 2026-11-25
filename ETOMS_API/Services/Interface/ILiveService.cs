using ETOMS_API.DTOs.Live;
using ETOMS_API.Utils;

namespace ETOMS_API.Services.Interface;

// UC-45 tỉ số trực tiếp (LIVE-01/02/04) và UC-46 livestream.
public interface ILiveService
{
    Task<Result<MatchLiveDto>> GetMatchLiveAsync(int matchId, int? viewerAccountId);
    Task<Result<MatchLiveDto>> SaveGameAsync(int matchId, int gameNo, int actorAccountId, SaveLiveGameRequest request);
    Task<Result<MatchLiveDto>> ClearGameAsync(int matchId, int gameNo, int actorAccountId);

    Task<Result<TournamentLiveDto>> GetTournamentStreamsAsync(int tournamentId, int? viewerAccountId);
    Task<Result<LiveStreamDto>> CreateStreamAsync(int tournamentId, int actorAccountId, SaveLivestreamRequest request);
    Task<Result<LiveStreamDto>> UpdateStreamAsync(int tournamentId, int livestreamId, int actorAccountId, SaveLivestreamRequest request);
    Task<Result> DeleteStreamAsync(int tournamentId, int livestreamId, int actorAccountId);
}
