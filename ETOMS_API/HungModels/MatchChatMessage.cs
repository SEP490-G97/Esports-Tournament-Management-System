using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.HungModels;

[Index("MatchID", "SentAt", Name = "IX_MatchChatMessage_Match")]
public partial class MatchChatMessage
{
    [Key]
    public int MatchChatMessageID { get; set; }

    public int MatchID { get; set; }

    public int SenderAccountID { get; set; }

    [StringLength(2000)]
    public string Content { get; set; } = null!;

    public bool IsPinned { get; set; }

    [StringLength(20)]
    [Unicode(false)]
    public string ReportStatus { get; set; } = null!;

    public int? ReportedByAccountID { get; set; }

    [StringLength(500)]
    public string? ReportNote { get; set; }

    [Precision(0)]
    public DateTime SentAt { get; set; }

    [ForeignKey("MatchID")]
    [InverseProperty("MatchChatMessage")]
    public virtual Match Match { get; set; } = null!;

    [ForeignKey("ReportedByAccountID")]
    [InverseProperty("MatchChatMessageReportedByAccount")]
    public virtual Account? ReportedByAccount { get; set; }

    [ForeignKey("SenderAccountID")]
    [InverseProperty("MatchChatMessageSenderAccount")]
    public virtual Account SenderAccount { get; set; } = null!;
}
