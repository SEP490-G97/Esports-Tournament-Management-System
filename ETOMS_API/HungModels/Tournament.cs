using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.HungModels;

[Index("OrganizationID", "Status", Name = "IX_Tournament_Organization")]
public partial class Tournament
{
    [Key]
    public int TournamentID { get; set; }

    public int GameID { get; set; }

    public int OrganizerAccountID { get; set; }

    [StringLength(200)]
    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    [StringLength(500)]
    public string? BannerUrl { get; set; }

    [StringLength(200)]
    public string? OrganizingUnit { get; set; }

    [StringLength(100)]
    public string? Region { get; set; }

    [StringLength(300)]
    public string? ContactInfo { get; set; }

    public bool IsOnline { get; set; }

    [StringLength(300)]
    public string? Venue { get; set; }

    [StringLength(30)]
    [Unicode(false)]
    public string Format { get; set; } = null!;

    public bool HasThirdPlaceMatch { get; set; }

    public int? GroupCount { get; set; }

    public int? AdvancePerGroup { get; set; }

    public int MaxTeams { get; set; }

    public int StarterCount { get; set; }

    public int MaxSubstitutes { get; set; }

    [Precision(0)]
    public DateTime RegOpenAt { get; set; }

    [Precision(0)]
    public DateTime RegCloseAt { get; set; }

    [Precision(0)]
    public DateTime StartAt { get; set; }

    [Precision(0)]
    public DateTime? EndAt { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal EntryFee { get; set; }

    [StringLength(300)]
    public string? BankAccountInfo { get; set; }

    public string? PrizeStructure { get; set; }

    public bool RequireIdentityDocument { get; set; }

    public bool RequirePaymentProof { get; set; }

    public bool RequireInGameName { get; set; }

    [StringLength(200)]
    public string? TieBreakerOrder { get; set; }

    [StringLength(200)]
    public string? BestOfPerRound { get; set; }

    public int? PenaltyThreshold { get; set; }

    public string? RulebookContent { get; set; }

    public int RulebookVersion { get; set; }

    [StringLength(500)]
    public string? StreamUrl { get; set; }

    [StringLength(20)]
    [Unicode(false)]
    public string Status { get; set; } = null!;

    [StringLength(500)]
    public string? CancelReason { get; set; }

    public int? BracketLockedByAccountID { get; set; }

    [Precision(0)]
    public DateTime? BracketLockedAt { get; set; }

    [Precision(0)]
    public DateTime CreatedAt { get; set; }

    public int? OrganizationID { get; set; }

    [InverseProperty("Tournament")]
    public virtual ICollection<AuditLog> AuditLog { get; set; } = new List<AuditLog>();

    [ForeignKey("BracketLockedByAccountID")]
    [InverseProperty("TournamentBracketLockedByAccount")]
    public virtual Account? BracketLockedByAccount { get; set; }

    [InverseProperty("Tournament")]
    public virtual ICollection<BracketNode> BracketNode { get; set; } = new List<BracketNode>();

    [InverseProperty("Tournament")]
    public virtual ICollection<ContentItem> ContentItem { get; set; } = new List<ContentItem>();

    [ForeignKey("GameID")]
    [InverseProperty("Tournament")]
    public virtual Game Game { get; set; } = null!;

    [InverseProperty("Tournament")]
    public virtual ICollection<Livestream> Livestream { get; set; } = new List<Livestream>();

    [InverseProperty("Tournament")]
    public virtual ICollection<Match> Match { get; set; } = new List<Match>();

    [ForeignKey("OrganizationID")]
    [InverseProperty("Tournament")]
    public virtual Organization? Organization { get; set; }

    [ForeignKey("OrganizerAccountID")]
    [InverseProperty("TournamentOrganizerAccount")]
    public virtual Account OrganizerAccount { get; set; } = null!;

    [InverseProperty("Tournament")]
    public virtual ICollection<Penalty> Penalty { get; set; } = new List<Penalty>();

    [InverseProperty("Tournament")]
    public virtual ICollection<PromotionLink> PromotionLink { get; set; } = new List<PromotionLink>();

    [InverseProperty("Tournament")]
    public virtual ICollection<Registration> Registration { get; set; } = new List<Registration>();

    [InverseProperty("Tournament")]
    public virtual ICollection<TournamentFollow> TournamentFollow { get; set; } = new List<TournamentFollow>();

    [InverseProperty("Tournament")]
    public virtual ICollection<TournamentSponsor> TournamentSponsor { get; set; } = new List<TournamentSponsor>();
}
