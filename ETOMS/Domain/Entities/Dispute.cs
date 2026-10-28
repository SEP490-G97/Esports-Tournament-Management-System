using System;
using System.Collections.Generic;

namespace ETOMS.Domain.Entities;

public partial class Dispute
{
    public int DisputeId { get; set; }

    public int MatchId { get; set; }

    public string OpenReason { get; set; } = null!;

    public int? FlaggedByAccountId { get; set; }

    public string? FlagCategory { get; set; }

    public string? FlagNote { get; set; }

    public string Status { get; set; } = null!;

    public int? FinalScoreA { get; set; }

    public int? FinalScoreB { get; set; }

    public string? RulingReason { get; set; }

    public int? ResolvedByAccountId { get; set; }

    public DateTime? ResolvedAt { get; set; }

    public int AppealCount { get; set; }

    public string? AppealReason { get; set; }

    public string AppealStatus { get; set; } = null!;

    public DateTime OpenedAt { get; set; }

    public virtual Account? FlaggedByAccount { get; set; }

    public virtual Match Match { get; set; } = null!;

    public virtual Account? ResolvedByAccount { get; set; }
}
