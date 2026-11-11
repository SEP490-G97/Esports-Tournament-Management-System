using ETOMS_API.DTOs.Tournament;
using ETOMS_API.Utils;

namespace ETOMS_API.Services.Interface;

public interface ITournamentCancelService
{
    Task<int?> GetOrganizerAccountIdAsync(int tournamentId);

    Task<TournamentCancelBoardDto?> GetCancelBoardAsync(int tournamentId);

    Task<Result<TournamentCancelBoardDto>> CancelAsync(int tournamentId, int organizerAccountId, string reason);
}
