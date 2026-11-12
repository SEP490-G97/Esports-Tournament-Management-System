using ETOMS_API.DTOs.Tournament;

namespace ETOMS_API.Services.Interface;

// TOUR-12 / UC-13 A3: Bảng điều khiển của một giải.
public interface ITournamentDashboardService
{
    // null = không có giải.
    Task<int?> GetOrganizerAccountIdAsync(int tournamentId);

    Task<TournamentDashboardDto?> GetDashboardAsync(int tournamentId);
}
