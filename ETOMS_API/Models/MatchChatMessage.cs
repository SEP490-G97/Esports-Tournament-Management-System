using System;
using System.Collections.Generic;

namespace ETOMS_API.Models;

// Khớp bảng MatchChatMessage trên DB chung (bảng đã đổi cấu trúc ngày 17/11): Body, SenderRole, TeamID.
public partial class MatchChatMessage
{
    public long MatchChatMessageId { get; set; }

    public int MatchId { get; set; }

    public int SenderAccountId { get; set; }

    /// <summary>
    /// Vai trò tại thời điểm gửi: CAPTAIN / ORGANIZER / REFEREE
    /// </summary>
    public string SenderRole { get; set; } = null!;

    /// <summary>
    /// Đội của đội trưởng (NULL với BTC / trọng tài)
    /// </summary>
    public int? TeamId { get; set; }

    public string Body { get; set; } = null!;

    public DateTime SentAt { get; set; }

    public virtual Match Match { get; set; } = null!;

    public virtual Account SenderAccount { get; set; } = null!;

    public virtual Team? Team { get; set; }
}
