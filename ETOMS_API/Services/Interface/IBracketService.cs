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

    // BRK-03: khóa nhánh đấu; mở khóa lại khi chưa có trận nào bắt đầu.
    Task<Result> LockBracketAsync(int tournamentId, int organizerAccountId);
    Task<Result> UnlockBracketAsync(int tournamentId, int organizerAccountId);

    // Đổi chỗ 2 đội trong nhánh đã sinh (chưa khóa, 2 đội chưa đấu trận nào).
    Task<Result> SwapTeamsAsync(int tournamentId, int teamAId, int teamBId, int organizerAccountId);

    // BRK-07: thứ tự ưu tiên tiêu chí phân định khi bằng điểm vòng bảng.
    Task<TieBreakerConfigDto?> GetTieBreakerConfigAsync(int tournamentId);
    Task<Result> SetTieBreakerConfigAsync(int tournamentId, List<string> order, int organizerAccountId);

    Task<bool> IsOrganizerAsync(int tournamentId, int accountId);
}
