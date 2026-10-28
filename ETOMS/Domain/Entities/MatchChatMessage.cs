using System;
using System.Collections.Generic;

namespace ETOMS.Domain.Entities;

public partial class MatchChatMessage
{
    public int MatchChatMessageId { get; set; }

    public int MatchId { get; set; }

    public int SenderAccountId { get; set; }

    public string Content { get; set; } = null!;

    public bool IsPinned { get; set; }

    public string ReportStatus { get; set; } = null!;

    public int? ReportedByAccountId { get; set; }

    public string? ReportNote { get; set; }

    public DateTime SentAt { get; set; }

    public virtual Match Match { get; set; } = null!;

    public virtual Account? ReportedByAccount { get; set; }

    public virtual Account SenderAccount { get; set; } = null!;
}
