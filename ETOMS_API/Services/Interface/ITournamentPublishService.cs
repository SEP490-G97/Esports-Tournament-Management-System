using ETOMS_API.DTOs.Tournament;
using ETOMS_API.Utils;

namespace ETOMS_API.Services.Interface;

// TOUR-09 / UC-14: Xuất bản và gỡ xuất bản giải.
public interface ITournamentPublishService
{
    // null = không có giải, false = có giải nhưng không phải Ban tổ chức của giải đó.
    Task<bool?> IsOrganizerAsync(int tournamentId, int accountId);

    Task<PublishStatusDto?> GetPublishStatusAsync(int tournamentId);

    Task<Result> PublishTournamentAsync(int tournamentId, int organizerAccountId);

    Task<Result> UnpublishTournamentAsync(int tournamentId, int organizerAccountId);
}
