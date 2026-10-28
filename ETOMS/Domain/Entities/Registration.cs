using System;
using System.Collections.Generic;

namespace ETOMS.Domain.Entities;

public partial class Registration
{
    public int RegistrationId { get; set; }

    public int TournamentId { get; set; }

    public int TeamId { get; set; }

    public int SubmittedByAccountId { get; set; }

    public int? RulebookVersionAccepted { get; set; }

    public DateTime? ConsentAt { get; set; }

    public string Status { get; set; } = null!;

    public int? WaitlistPosition { get; set; }

    public string PaymentStatus { get; set; } = null!;

    public string? ReviewNote { get; set; }

    public string FinalPlacement { get; set; } = null!;

    public int? FinalRank { get; set; }

    public DateTime? SubmittedAt { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public virtual PaymentRecord? PaymentRecord { get; set; }

    public virtual ICollection<RegistrationLineup> RegistrationLineups { get; set; } = new List<RegistrationLineup>();

    public virtual Account SubmittedByAccount { get; set; } = null!;

    public virtual Team Team { get; set; } = null!;

    public virtual Tournament Tournament { get; set; } = null!;
}
