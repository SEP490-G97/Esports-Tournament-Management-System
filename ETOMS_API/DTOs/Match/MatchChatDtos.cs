namespace ETOMS_API.DTOs;

public record ChatSendBody(string? Text);

public record ChatMessageDto(long Id, int MatchId, int SenderId, string SenderName, string SenderRole, int? TeamId, string? TeamName, string Text, DateTime SentAt);

public record ChatParticipant(int AccountId, string Name, string Role, int? TeamId, string? TeamName);

/// <summary>Lịch sử chat + trạng thái phòng. Messages sắp tăng dần theo Id.</summary>
public record ChatHistory(
    int MatchId,
    string MatchStatus,
    string TeamAName,
    string TeamBName,
    string MyRole,            // CAPTAIN / ORGANIZER / REFEREE / ADMIN
    int? MyTeamId,
    bool CanSend,
    string? ClosedReason,     // LOBBY_CLOSED khi trận đã kết thúc / hủy; READ_ONLY khi admin
    bool HasMore,
    DateTime ServerNow,
    IReadOnlyList<ChatParticipant> Participants,
    IReadOnlyList<ChatMessageDto> Messages);