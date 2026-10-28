using System;
using System.Collections.Generic;

namespace ETOMS.Domain.Entities;

public partial class Tournament
{
    public int TournamentId { get; set; }

    public int GameId { get; set; }

    public int OrganizerAccountId { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public string? BannerUrl { get; set; }

    public string? OrganizingUnit { get; set; }

    public string? Region { get; set; }

    public string? ContactInfo { get; set; }

    public bool IsOnline { get; set; }

    public string? Venue { get; set; }

    public string Format { get; set; } = null!;

    public bool HasThirdPlaceMatch { get; set; }

    public int MaxTeams { get; set; }

    public int StarterCount { get; set; }

    public int MaxSubstitutes { get; set; }

    public DateTime RegOpenAt { get; set; }

    public DateTime RegCloseAt { get; set; }

    public DateTime StartAt { get; set; }

    public DateTime? EndAt { get; set; }

    public decimal EntryFee { get; set; }

    public string? BankAccountInfo { get; set; }

    public string? PrizeStructure { get; set; }

    public bool RequireStudentCard { get; set; }

    public bool RequirePaymentProof { get; set; }

    public bool RequireInGameName { get; set; }

    public string? TieBreakerOrder { get; set; }

    public string? BestOfPerRound { get; set; }

    public int? PenaltyThreshold { get; set; }

    public string? RulebookContent { get; set; }

    public int RulebookVersion { get; set; }

    public string? StreamUrl { get; set; }

    public string Status { get; set; } = null!;

    public string? CancelReason { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();

    public virtual ICollection<BracketNode> BracketNodes { get; set; } = new List<BracketNode>();

    public virtual Game Game { get; set; } = null!;

    public virtual ICollection<Livestream> Livestreams { get; set; } = new List<Livestream>();

    public virtual ICollection<Match> Matches { get; set; } = new List<Match>();

    public virtual Account OrganizerAccount { get; set; } = null!;

    public virtual ICollection<Penalty> Penalties { get; set; } = new List<Penalty>();

    public virtual ICollection<Registration> Registrations { get; set; } = new List<Registration>();

    public virtual ICollection<TournamentFollow> TournamentFollows { get; set; } = new List<TournamentFollow>();

    public virtual ICollection<TournamentSponsor> TournamentSponsors { get; set; } = new List<TournamentSponsor>();
}
