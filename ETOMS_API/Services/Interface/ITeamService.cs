using ETOMS_API.Utils;
using ETOMS_API.DTOs.Team;

namespace ETOMS_API.Services.Interface;

public interface ITeamService
{
    // TEAM-01: tạo đội. Trả về TeamId mới nếu thành công, hoặc lỗi theo field.
    Task<Result<int>> CreateTeamAsync(CreateTeamRequest request, int captainAccountId);

    // Danh sách đội — có tìm kiếm/lọc/sắp xếp (q, gameId, status, sort).
    Task<IReadOnlyList<TeamListItemDto>> GetAllTeamsAsync(TeamSearchRequest? filter = null);
    Task<TeamDetailDto?> GetTeamDetailAsync(int teamId);

    // TEAM-02: trang quản trị đội (chỉ đội trưởng)
    Task<TeamDashboardDto?> GetDashboardAsync(int teamId);

    // Trả về AccountId của đội trưởng (null nếu đội không tồn tại) — kiểm tra quyền.
    Task<int?> GetCaptainAccountIdAsync(int teamId);

    // TEAM-04: mã mời (1 mã trên Team)
    Task<InviteInfoDto> GetInviteAsync(int teamId);
    Task<Result<string>> GenerateInviteAsync(int teamId, int captainAccountId, CreateInviteRequest request);
    Task<Result> RevokeInviteAsync(int teamId, int captainAccountId);

    // TEAM-05: gia nhập đội bằng mã. Trả về TeamId đã vào.
    Task<Result<int>> JoinByCodeAsync(string code, int accountId);

    // TEAM-03: quản lý roster
    Task<RosterDto?> GetRosterAsync(int teamId);
    Task<Result> ChangeSlotAsync(int teamId, int targetAccountId, string newSlot, int captainAccountId);
    Task<Result> KickMemberAsync(int teamId, int targetAccountId, int captainAccountId);

    // Duyệt / từ chối đơn ứng tuyển - lời mời đang chờ (TeamMember.Status = PENDING).
    Task<Result> ApproveMemberAsync(int teamId, int targetAccountId, int captainAccountId);
    Task<Result> RejectMemberAsync(int teamId, int targetAccountId, int captainAccountId);

    // TEAM-07: chuyển giao quyền đội trưởng
    Task<Result> TransferCaptainAsync(int teamId, int newCaptainAccountId, int currentCaptainAccountId);

    // TEAM-06: rời đội
    Task<Result> LeaveTeamAsync(int teamId, int accountId);

    // TEAM-08: giải tán đội
    Task<Result> DisbandTeamAsync(int teamId, int captainAccountId);

    // Người dùng có đang là thành viên ACTIVE không.
    Task<bool> IsActiveMemberAsync(int teamId, int accountId);

    // Danh sách tựa game đang mở (dropdown).
    Task<IReadOnlyList<GameOptionDto>> GetActiveGamesAsync();
}
