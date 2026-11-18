using ETOMS_API.DTOs.Registration;
using ETOMS_API.Utils;

namespace ETOMS_API.Services.Interface;

// GRL-04: Đội trưởng gán vị trí cho từng người trong đội hình đã khai ở đơn đăng ký.
public interface ILineupPositionService
{
    // null = không có đơn đăng ký.
    Task<int?> GetCaptainAccountIdAsync(int registrationId);

    Task<Result<LineupPositionBoardDto>> GetBoardAsync(int registrationId);

    Task<Result<LineupPositionBoardDto>> SavePositionsAsync(int registrationId, SaveLineupPositionsRequest request);
}
