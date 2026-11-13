using ETOMS_API.DTOs.Bracket;

namespace ETOMS_API.Services.Interface;

public interface IGroupStandingService
{
    // null khi không có giải, hoặc giải còn nháp mà người xem không phải Ban tổ chức.
    Task<GroupStandingBoardDto?> GetStandingsAsync(int tournamentId, int? viewerAccountId);
}
