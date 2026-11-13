using ETOMS_API.DTOs.Tournament;
using ETOMS_API.Utils;

namespace ETOMS_API.Services.Interface;

public interface ITournamentSettingsService
{
    Task<int?> GetOrganizerAccountIdAsync(int tournamentId);

    Task<TournamentSettingsBoardDto?> GetSettingsAsync(int tournamentId);

    Task<Result<TournamentSettingsBoardDto>> SaveSettingsAsync(int tournamentId, TournamentSettingsValues values);
}
