
using ETOMS_API.DTOs;

namespace ETOMS_API.Services.Interface;

public enum ManualCheckInError
{
    None, NotFound, Forbidden, InvalidReason, InvalidTeams, TournamentClosed,
    InvalidStatus, AlreadyCheckedIn, TeamNotAbsent, RollbackBlocked
}

public record ManualCheckInResult(ManualCheckInError Error, ManualCheckInResponse? Data = null, string? Detail = null);

public interface IManualCheckInService
{
    /// <summary>
    /// MAT-04 — BTC (hoặc ADMIN) check-in thủ công cho đội. Trận READY: đánh dấu đội đã check-in. Trận đã bị cờ No-show
    /// (WALKOVER do hệ thống / DISPUTED vì cả hai vắng): gỡ cờ, hoàn tác thắng trắng, cho thi đấu tiếp. Luôn ghi AuditLog
    /// (MANUAL_CHECKIN) kèm lý do, và đặt Match.CheckInMode = MANUAL_OVERRIDE.
    /// </summary>
    Task<ManualCheckInResult> ApplyAsync(int matchId, int actorId, string? actorRole, ManualCheckInRequest request, CancellationToken ct = default);
}