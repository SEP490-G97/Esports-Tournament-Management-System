using ETOMS_API.DTOs;

namespace ETOMS_API.Services.Interface;

public enum SubstitutionError
{
    None, NotFound, Forbidden, NotCaptain, MatchNotEditable, WindowClosed, NoRegistration,
    InvalidPlayers, PlayerBusy, QuotaExceeded, InvalidReason, NoteRequired, NotPending, NotDecider
}

public record SubstitutionResult<T>(SubstitutionError Error, T? Data = default);

public interface ISubstitutionService
{
    /// <summary>Đội hình active, hạn mức thay và các yêu cầu. Chỉ thành viên 2 đội, trọng tài, BTC của giải hoặc ADMIN xem được.</summary>
    Task<SubstitutionResult<SubstitutionBoard>> GetBoardAsync(int matchId, int actorId, string? actorRole, CancellationToken ct = default);

    /// <summary>MAT-05: đội trưởng gửi yêu cầu thay tuyển thủ chính bằng dự bị, trước giờ thi đấu và trong hạn mức của Rule giải.</summary>
    Task<SubstitutionResult<SubstitutionItem>> RequestAsync(int matchId, int actorId, SubstitutionRequestBody body, CancellationToken ct = default);

    /// <summary>Đội trưởng rút yêu cầu đang chờ.</summary>
    Task<SubstitutionResult<SubstitutionItem>> CancelAsync(int matchId, int substitutionId, int actorId, CancellationToken ct = default);

    /// <summary>MAT-06: trọng tài của trận duyệt / từ chối; trận không có trọng tài thì BTC của giải duyệt (fallback). ADMIN cũng được.</summary>
    Task<SubstitutionResult<SubstitutionItem>> DecideAsync(int matchId, int substitutionId, int actorId, string? actorRole,
        SubstitutionDecisionBody body, CancellationToken ct = default);
}