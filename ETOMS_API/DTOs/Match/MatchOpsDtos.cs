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
    // Phần đầu trang trận (bảng tỉ số): logo, tag đội, tên giải, game.
    public string? TeamALogoUrl { get; set; }
    public string? TeamBLogoUrl { get; set; }
    public string? TeamATag { get; set; }
    public string? TeamBTag { get; set; }
    public string TournamentName { get; set; } = string.Empty;
    public string? GameTitle { get; set; }
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
    // UC-25 bước 3: đội trưởng đội nhà (đội A) cũng được nhập mã phòng, cùng trọng tài/BTC.
    public bool CanSetRoom { get; set; }
    // UC-21: cửa sổ điểm danh (mở 30 phút trước giờ thi đấu, đóng đúng giờ thi đấu).
    public DateTime? CheckInOpenAt { get; set; }
    public bool MyTeamCheckedIn { get; set; }

    // Khối "Kết quả trận" (góp ý 22/11: trọng tài không thấy chỗ nhập tỉ số). Theo LI-01 tỉ số chỉ vào qua 2 báo cáo mù
    // của 2 đội trưởng hoặc phán quyết tranh chấp — trang chỉ cho biết ĐỘI NÀO đã nộp, không lộ tỉ số đã khai.
    public bool TeamAReported { get; set; }
    public bool TeamBReported { get; set; }
    public int? OpenDisputeId { get; set; }
    // Trận đã có phán quyết tranh chấp (trọng tài/BTC chốt tỉ số): ai xem trận cũng thấy được lịch sử phán quyết.
    public bool HasRuling { get; set; }
    // Trận bị hủy kết quả sau phán quyết tranh chấp (VAR_RULING): BTC/trọng tài phải xử thắng cho một đội hoặc cho đấu lại
    // thì nhánh mới đi tiếp (mục 15, 23/11).
    public bool AwaitingDecision { get; set; }
    // Trận bị hủy, xử thua cả hai đội (CANCELLED + ResultSource WALKOVER, không đội thắng).
    public bool DoubleLoss { get; set; }
    public bool IsReferee { get; set; }
    public string? RefereeName { get; set; }
    public DateTime? ReportDeadlineAt { get; set; }

    // Form "Xin thay người": đội trưởng chọn theo TÊN thành viên đang hoạt động của đội mình, không phải gõ mã tài khoản.
    public List<MatchTeamMemberOption> MyTeamMembers { get; set; } = new();
}

public class MatchTeamMemberOption
{
    public int AccountId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Slot { get; set; } = string.Empty; // STARTER | SUBSTITUTE
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
    public long MatchChatMessageId { get; set; }
    public int SenderAccountId { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
    // UC-25 A1: REFEREE | ORGANIZER | CAPTAIN_A | CAPTAIN_B | FORMER (không còn vai trò trong trận)
    public string SenderRole { get; set; } = string.Empty;
    public string SenderRoleLabel { get; set; } = string.Empty;
    public bool IsMine { get; set; }
    // UC-25 A2: NONE | REPORTED | ACTIONED — chỉ Ban tổ chức/trọng tài và người báo cáo thấy trạng thái báo cáo.
    public string ReportStatus { get; set; } = "NONE";
    public bool CanReport { get; set; }
}

// UC-25 — phòng chờ trận: trạng thái mở/đóng + tin nhắn.
public class MatchLobbyDto
{
    public bool IsOpen { get; set; }          // gửi được tin nhắn
    public string StateMessage { get; set; } = string.Empty;
    public string MyRoleLabel { get; set; } = string.Empty;
    public int MaxLength { get; set; }
    public List<ChatMessageDto> Messages { get; set; } = new();
}

public class SendChatMessageRequest
{
    public string Content { get; set; } = string.Empty;
}

public class ReportChatMessageRequest
{
    public string Note { get; set; } = string.Empty;
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

// Hủy trận, xử thua cả hai đội (góp ý 25/11).
public class CancelMatchRequest
{
    public string Reason { get; set; } = string.Empty;
}

// UC-26 A2: một trận phía sau đã đá sẽ bị hủy kết quả khi cho trận hiện tại đấu lại.
public class RematchImpactDto
{
    public int MatchId { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
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
