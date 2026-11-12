using ETOMS_API.DTOs.Bracket;

namespace ETOMS_API.Services.Interface;

public interface IBracketViewService
{
    // null khi không có giải, hoặc giải còn nháp mà người xem không phải Ban tổ chức.
    Task<BracketViewDto?> GetBracketAsync(int tournamentId, int? viewerAccountId);
}
