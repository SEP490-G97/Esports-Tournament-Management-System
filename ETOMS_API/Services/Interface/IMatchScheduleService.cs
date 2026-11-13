using ETOMS_API.DTOs.Bracket;
using ETOMS_API.Utils;

namespace ETOMS_API.Services.Interface;

public interface IMatchScheduleService
{
    Task<int?> GetOrganizerAccountIdAsync(int tournamentId);

    Task<MatchScheduleBoardDto?> GetBoardAsync(int tournamentId);

    // scheduledAt null = bỏ giờ đã xếp.
    Task<Result<MatchScheduleBoardDto>> ScheduleMatchAsync(int tournamentId, int matchId, DateTime? scheduledAt);

    Task<Result<ScheduleRoundResultDto>> ScheduleRoundAsync(int tournamentId, ScheduleRoundRequest request);
}
