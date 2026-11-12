using ETOMS_API.DTOs.Tournament;
using ETOMS_API.Utils;

namespace ETOMS_API.Services.Interface;

// TOUR-13 / UC-17: Gán trọng tài cho từng trận hoặc cả một vòng.
public interface IRefereeAssignmentService
{
    // null = không có giải.
    Task<int?> GetOrganizerAccountIdAsync(int tournamentId);

    Task<RefereeBoardDto?> GetBoardAsync(int tournamentId);

    Task<IReadOnlyList<RefereeCandidateDto>> SearchCandidatesAsync(string? keyword);

    Task<Result> AssignRefereeAsync(int tournamentId, int matchId, int? refereeAccountId);

    Task<Result<AssignRoundResultDto>> AssignRoundAsync(int tournamentId, AssignRoundRefereeRequest request);
}
