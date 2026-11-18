namespace ETOMS_API.DTOs;

/// <summary>TeamIds: một hoặc hai đội cần check-in thủ công. Reason: bắt buộc, 10–500 ký tự (ghi vào Audit Log và Match.OverrideReason).</summary>
public record ManualCheckInRequest(List<int>? TeamIds, string? Reason);

public record ManualCheckInResponse(
    int MatchId,
    string Status,
    IReadOnlyList<int> CheckedInTeamIds,
    /// <summary>Đã gỡ cờ No-show (trận từng bị xử thắng trắng / đình chỉ) và cho thi đấu tiếp.</summary>
    bool NoShowFlagRemoved,
    /// <summary>Đội vẫn chưa check-in sau thao tác (trận vẫn có thể bị quét No-show nếu đã quá giờ).</summary>
    IReadOnlyList<int> StillAbsentTeamIds);