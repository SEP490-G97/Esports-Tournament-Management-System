using ETOMS_API.DTOs.Ranking;
using ETOMS_API.Utils;

namespace ETOMS_API.Services.Interface;

public interface IRankingService
{
    Task<IReadOnlyList<LeaderboardEntryResponse>> GetLeaderboardAsync(int? gameId, int? season, CancellationToken cancellationToken);
    Task<IReadOnlyList<int>> GetSeasonsAsync(int? gameId, CancellationToken cancellationToken);
    Task<Result<RankedTeamProfileResponse>> GetTeamProfileAsync(int teamId, CancellationToken cancellationToken);
    Task<Result<TournamentReportResponse>> GetTournamentReportAsync(int tournamentId, int accountId, CancellationToken cancellationToken);
    Task FinalizeCompletedTournamentsAsync(CancellationToken cancellationToken);
}
