using System;
using System.Collections.Generic;

namespace ETOMS.Domain.Entities;

public partial class MatchRequest
{
    public int MatchRequestId { get; set; }

    public int MatchId { get; set; }

    public int? TeamId { get; set; }

    public string RequestKind { get; set; } = null!;

    public int? OutAccountId { get; set; }

    public int? InAccountId { get; set; }

    public string Reason { get; set; } = null!;

    public string Status { get; set; } = null!;

    public int RequestedByAccountId { get; set; }

    public int? DecidedByAccountId { get; set; }

    public DateTime? DecidedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Account? DecidedByAccount { get; set; }

    public virtual Account? InAccount { get; set; }

    public virtual Match Match { get; set; } = null!;

    public virtual Account? OutAccount { get; set; }

    public virtual Account RequestedByAccount { get; set; } = null!;

    public virtual Team? Team { get; set; }
}
