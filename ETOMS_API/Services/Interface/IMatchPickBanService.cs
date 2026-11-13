using ETOMS_API.DTOs.PickBan;
using ETOMS_API.Utils;

namespace ETOMS_API.Services.Interface;

public interface IMatchPickBanService
{
    // null khi không có trận; true khi tài khoản là trọng tài của trận hoặc Ban tổ chức của giải.
    Task<bool?> CanRecordAsync(int matchId, int accountId);

    Task<Result<MatchPickBanBoardDto>> GetBoardAsync(int matchId, int? viewerAccountId);

    Task<Result<MatchPickBanBoardDto>> SaveAsync(int matchId, int accountId, SaveMatchPickBanRequest request);
}
