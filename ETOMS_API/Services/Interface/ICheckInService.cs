
using ETOMS_API.DTOs;

namespace ETOMS_API.Services.Interface;

public enum CheckInError { None, NotFound, Forbidden, NotReady, NotScheduled, NotYetOpen, Closed, AlreadyCheckedIn }

public record CheckInResult(CheckInError Error, CheckInState? Data = null);

public interface ICheckInService
{
    /// <summary>Chỉ đội trưởng của một trong hai đội mới xem được (đội khác: Forbidden).</summary>
    Task<CheckInResult> GetStateAsync(int matchId, int actorId, CancellationToken ct = default);

    /// <summary>Đội trưởng check-in cho đội mình. Chỉ khi trận READY, đủ hai đội, trong khoảng [giờ đấu − 30 phút, giờ đấu).</summary>
    Task<CheckInResult> CheckInAsync(int matchId, int actorId, CancellationToken ct = default);
}