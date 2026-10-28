using System;
using System.Collections.Generic;

namespace ETOMS.Domain.Entities;

public partial class Penalty
{
    public int PenaltyId { get; set; }

    public int TournamentId { get; set; }

    public int TeamId { get; set; }

    public int? MatchId { get; set; }

    public string Severity { get; set; } = null!;

    public int Points { get; set; }

    public string Reason { get; set; } = null!;

    public int IssuedByAccountId { get; set; }

    public string Status { get; set; } = null!;

    public int? CancelsPenaltyId { get; set; }

    public string? CancelReason { get; set; }

    public DateTime IssuedAt { get; set; }

    public virtual Penalty? CancelsPenalty { get; set; }

    public virtual Penalty? InverseCancelsPenalty { get; set; }

    public virtual Account IssuedByAccount { get; set; } = null!;

    public virtual Match? Match { get; set; }

    public virtual Team Team { get; set; } = null!;

    public virtual Tournament Tournament { get; set; } = null!;
}
