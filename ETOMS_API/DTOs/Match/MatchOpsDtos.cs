namespace ETOMS_API.DTOs.Match;

// STUB TẠM thay module MAT (Vũ Đức Hải: MAT-01/04/05/06/07, Nguyễn Quang Anh: MAT-08..12) —
// chỉ đủ để demo luồng vào trận → check-in → thắng trắng/rematch/disqualify/forfeit. Cần hai bạn
// thay bằng bản thật của mình, đây không phải bài làm cá nhân của họ.

public class MatchDetailDto
{
    public int MatchId { get; set; }
    public int TournamentId { get; set; }
    public int? TeamAId { get; set; }
    public string TeamAName { get; set; } = string.Empty;
    public int? TeamBId { get; set; }
    public string TeamBName { get; set; } = string.Empty;
    public int BestOf { get; set; }
    public DateTime? ScheduledAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public int? ScoreA { get; set; }
    public int? ScoreB { get; set; }
    public int? WinnerTeamId { get; set; }

    // Chỉ điền khi người xem có quyền (2 đội trưởng của trận, trọng tài, BTC) — MAT-08.
    public string? RoomCode { get; set; }
    public string? RoomPassword { get; set; }

    public bool TeamACheckedIn { get; set; }
    public bool TeamBCheckedIn { get; set; }
    public bool CanManage { get; set; } // trọng tài/BTC của giải
    public int? MyCaptainTeamId { get; set; } // null nếu người xem không phải đội trưởng của 1 trong 2 đội
}

public class ManualCheckInRequest
{
    public string TeamSide { get; set; } = string.Empty; // "A" | "B"
    public string Reason { get; set; } = string.Empty;
}

public class SetRoomInfoRequest
{
    public string RoomCode { get; set; } = string.Empty;
    public string? RoomPassword { get; set; }
}

public class SubstituteRequestDto
{
    public int MatchRequestId { get; set; }
    public int TeamId { get; set; }
    public int OutAccountId { get; set; }
    public string OutName { get; set; } = string.Empty;
    public int InAccountId { get; set; }
    public string InName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class CreateSubstituteRequest
{
    public int TeamId { get; set; }
    public int OutAccountId { get; set; }
    public int InAccountId { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class ChatMessageDto
{
    public int MatchChatMessageId { get; set; }
    public int SenderAccountId { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
}

public class SendChatMessageRequest
{
    public string Content { get; set; } = string.Empty;
}

public class WalkoverRequest
{
    public int WinnerTeamId { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class RematchRequest
{
    public string Reason { get; set; } = string.Empty;
}

public class DisqualifyRequest
{
    public int TeamId { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class ForfeitRequest
{
    public int TeamId { get; set; }
    public string Reason { get; set; } = string.Empty;
}
