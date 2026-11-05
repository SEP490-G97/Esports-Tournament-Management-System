using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.HungModels;

public partial class Dispute
{
    [Key]
    public int DisputeID { get; set; }

    public int MatchID { get; set; }

    [StringLength(30)]
    [Unicode(false)]
    public string OpenReason { get; set; } = null!;

    public int? FlaggedByAccountID { get; set; }

    [StringLength(30)]
    [Unicode(false)]
    public string? FlagCategory { get; set; }

    [StringLength(1000)]
    public string? FlagNote { get; set; }

    [StringLength(20)]
    [Unicode(false)]
    public string Status { get; set; } = null!;

    public int? FinalScoreA { get; set; }

    public int? FinalScoreB { get; set; }

    [StringLength(1000)]
    public string? RulingReason { get; set; }

    public int? ResolvedByAccountID { get; set; }

    [Precision(0)]
    public DateTime? ResolvedAt { get; set; }

    public int AppealCount { get; set; }

    [StringLength(1000)]
    public string? AppealReason { get; set; }

    [StringLength(20)]
    [Unicode(false)]
    public string AppealStatus { get; set; } = null!;

    [StringLength(1000)]
    public string? AppealOutcomeNote { get; set; }

    [Precision(0)]
    public DateTime OpenedAt { get; set; }

    [ForeignKey("FlaggedByAccountID")]
    [InverseProperty("DisputeFlaggedByAccount")]
    public virtual Account? FlaggedByAccount { get; set; }

    [ForeignKey("MatchID")]
    [InverseProperty("Dispute")]
    public virtual Match Match { get; set; } = null!;

    [ForeignKey("ResolvedByAccountID")]
    [InverseProperty("DisputeResolvedByAccount")]
    public virtual Account? ResolvedByAccount { get; set; }
}
