using ETOMS_API.DTOs.Bracket;
using ETOMS_API.Utils;

namespace ETOMS_API.Services.Interface;

public interface IStageTransitionService
{
    Task<int?> GetOrganizerAccountIdAsync(int tournamentId);

    // decisions: chỉ để xem trước khi Ban tổ chức đang xếp các đội hòa, không lưu gì.
    Task<Result<StageTransitionBoardDto>> GetBoardAsync(int tournamentId, AdvanceStageRequest? decisions = null);

    // Ban tổ chức bấm chuyển giai đoạn (bắt buộc khi có đội hòa cần quyết định).
    Task<Result<StageTransitionBoardDto>> AdvanceAsync(int tournamentId, AdvanceStageRequest request);

    // Tác vụ chạy nền: tự chuyển mọi giải HYBRID đã xong vòng bảng và không có đội hòa cần quyết định.
    Task<int> RunScheduledAsync(CancellationToken cancellationToken);
}
