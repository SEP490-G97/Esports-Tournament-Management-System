using ETOMS_API.Utils;
using ETOMS_API.DTOs.Bracket;

namespace ETOMS_API.Services.Interface;

public interface IBracketService
{
    // Danh sách đội đã duyệt (APPROVED), theo thứ tự duyệt — dùng cho màn xếp hạt giống.
    Task<IReadOnlyList<SeedingTeamDto>> GetApprovedTeamsAsync(int tournamentId);

    // BRK-01 + BRK-02 + BRK-04 + BRK-06: sinh nhánh đấu (single elimination hoặc round robin
    // tùy Tournament.Format). Idempotent khi bracket chưa khóa — gọi lại sẽ xóa cây cũ, dựng lại.
    Task<Result> GenerateBracketAsync(int tournamentId, GenerateBracketRequest request, int organizerAccountId);

    Task<BracketTreeDto?> GetBracketAsync(int tournamentId);

    Task<bool> IsOrganizerAsync(int tournamentId, int accountId);
}
