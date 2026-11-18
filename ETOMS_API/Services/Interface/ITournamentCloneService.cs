using ETOMS_API.DTOs.Tournament;
using ETOMS_API.Utils;

namespace ETOMS_API.Services.Interface;

// TOUR-14: Nhân bản giải từ mùa trước.
public interface ITournamentCloneService
{
    // null = không có giải.
    Task<int?> GetOrganizerAccountIdAsync(int tournamentId);

    // Trả về mã giải mới.
    Task<Result<int>> CloneTournamentAsync(int sourceTournamentId, CloneTournamentRequest request);
}
