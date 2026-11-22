using ETOMS_API.DTOs.Discipline;
using ETOMS_API.Utils;

namespace ETOMS_API.Services.Interface;

// Module PEN — UC-34 (cấp / hủy thẻ phạt), UC-35 (tước quyền khi đủ ngưỡng điểm phạt) và bộ loại đội dùng chung với UC-24.
public interface IDisciplineService
{
    Task<Result<DisciplineBoardDto>> GetBoardAsync(int tournamentId, int viewerAccountId, CancellationToken ct = default);
    Task<Result> IssueAsync(int tournamentId, int actorAccountId, IssuePenaltyRequest request, CancellationToken ct = default);
    Task<Result> CancelAsync(int tournamentId, int penaltyId, int actorAccountId, CancelPenaltyRequest request, CancellationToken ct = default);
    Task<Result> SetThresholdAsync(int tournamentId, int actorAccountId, SetPenaltyThresholdRequest request, CancellationToken ct = default);

    // Hồ sơ kỷ luật công khai của đội (mọi giải) — UC-34 hậu điều kiện, BR-PEN-01.
    Task<TeamDisciplineRecordDto> GetTeamRecordAsync(int teamId, CancellationToken ct = default);

    // Loại đội khỏi giải: đơn → DISQUALIFIED, mọi trận còn lại → thắng trắng cho đối thủ (cùng engine UC-23),
    // ghi một dòng nhật ký, báo đội trưởng, BTC và đối thủ. Trả số trận đã xử.
    Task<int> DisqualifyTeamAsync(int tournamentId, int teamId, int? actorAccountId, string reason, string auditAfterValue,
        int? alreadyResolvedMatchId = null, CancellationToken ct = default);

    // UC-35 (Scheduler): xét ngưỡng mọi giải đang diễn ra + xử nốt trận còn lại của đội đã bị loại khi đối thủ đã rõ.
    Task<int> RunScheduledAsync(CancellationToken ct = default);
}
