using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.HungModels;

[Index("Email", Name = "UQ_Account_Email", IsUnique = true)]
public partial class Account
{
    [Key]
    public int AccountID { get; set; }

    [StringLength(256)]
    public string Email { get; set; } = null!;

    [StringLength(255)]
    [Unicode(false)]
    public string? PasswordHash { get; set; }

    [StringLength(100)]
    [Unicode(false)]
    public string? GoogleSubjectId { get; set; }

    [StringLength(150)]
    public string FullName { get; set; } = null!;

    [StringLength(500)]
    public string? AvatarUrl { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    [StringLength(20)]
    [Unicode(false)]
    public string? Phone { get; set; }

    [StringLength(500)]
    public string? SocialLinks { get; set; }

    [StringLength(20)]
    [Unicode(false)]
    public string Role { get; set; } = null!;

    [StringLength(20)]
    [Unicode(false)]
    public string Status { get; set; } = null!;

    [StringLength(500)]
    public string? BanReason { get; set; }

    public bool NotifyInApp { get; set; }

    public bool NotifyEmail { get; set; }

    [StringLength(20)]
    [Unicode(false)]
    public string OrganizerRequestStatus { get; set; } = null!;

    [Precision(0)]
    public DateTime? OrganizerRequestedAt { get; set; }

    [Precision(0)]
    public DateTime? EmailVerifiedAt { get; set; }

    [Precision(0)]
    public DateTime CreatedAt { get; set; }

    [InverseProperty("UploadedByAccount")]
    public virtual ICollection<Attachment> Attachment { get; set; } = new List<Attachment>();

    [InverseProperty("ActorAccount")]
    public virtual ICollection<AuditLog> AuditLog { get; set; } = new List<AuditLog>();

    [InverseProperty("Account")]
    public virtual ICollection<AuthToken> AuthToken { get; set; } = new List<AuthToken>();

    [InverseProperty("AuthorAccount")]
    public virtual ICollection<ContentItem> ContentItem { get; set; } = new List<ContentItem>();

    [InverseProperty("FlaggedByAccount")]
    public virtual ICollection<Dispute> DisputeFlaggedByAccount { get; set; } = new List<Dispute>();

    [InverseProperty("ResolvedByAccount")]
    public virtual ICollection<Dispute> DisputeResolvedByAccount { get; set; } = new List<Dispute>();

    [InverseProperty("CreatedByAccount")]
    public virtual ICollection<Livestream> Livestream { get; set; } = new List<Livestream>();

    [InverseProperty("RefereeAccount")]
    public virtual ICollection<Match> Match { get; set; } = new List<Match>();

    [InverseProperty("ReportedByAccount")]
    public virtual ICollection<MatchChatMessage> MatchChatMessageReportedByAccount { get; set; } = new List<MatchChatMessage>();

    [InverseProperty("SenderAccount")]
    public virtual ICollection<MatchChatMessage> MatchChatMessageSenderAccount { get; set; } = new List<MatchChatMessage>();

    [InverseProperty("UpdatedByAccount")]
    public virtual ICollection<MatchGame> MatchGame { get; set; } = new List<MatchGame>();

    [InverseProperty("ReportedByAccount")]
    public virtual ICollection<MatchReport> MatchReport { get; set; } = new List<MatchReport>();

    [InverseProperty("DecidedByAccount")]
    public virtual ICollection<MatchRequest> MatchRequestDecidedByAccount { get; set; } = new List<MatchRequest>();

    [InverseProperty("InAccount")]
    public virtual ICollection<MatchRequest> MatchRequestInAccount { get; set; } = new List<MatchRequest>();

    [InverseProperty("OutAccount")]
    public virtual ICollection<MatchRequest> MatchRequestOutAccount { get; set; } = new List<MatchRequest>();

    [InverseProperty("RequestedByAccount")]
    public virtual ICollection<MatchRequest> MatchRequestRequestedByAccount { get; set; } = new List<MatchRequest>();

    [InverseProperty("Account")]
    public virtual ICollection<Notification> Notification { get; set; } = new List<Notification>();

    [InverseProperty("CreatedByAccount")]
    public virtual ICollection<Organization> OrganizationCreatedByAccount { get; set; } = new List<Organization>();

    [InverseProperty("Account")]
    public virtual ICollection<OrganizationMember> OrganizationMember { get; set; } = new List<OrganizationMember>();

    [InverseProperty("ReviewedByAccount")]
    public virtual ICollection<Organization> OrganizationReviewedByAccount { get; set; } = new List<Organization>();

    [InverseProperty("RecordedByAccount")]
    public virtual ICollection<PaymentRecord> PaymentRecord { get; set; } = new List<PaymentRecord>();

    [InverseProperty("IssuedByAccount")]
    public virtual ICollection<Penalty> Penalty { get; set; } = new List<Penalty>();

    [InverseProperty("Account")]
    public virtual ICollection<PlayerCredential> PlayerCredential { get; set; } = new List<PlayerCredential>();

    [InverseProperty("CreatedByAccount")]
    public virtual ICollection<PromotionLink> PromotionLink { get; set; } = new List<PromotionLink>();

    [InverseProperty("AuthorAccount")]
    public virtual ICollection<RecruitmentPost> RecruitmentPost { get; set; } = new List<RecruitmentPost>();

    [InverseProperty("SubmittedByAccount")]
    public virtual ICollection<Registration> Registration { get; set; } = new List<Registration>();

    [InverseProperty("Account")]
    public virtual ICollection<RegistrationLineup> RegistrationLineup { get; set; } = new List<RegistrationLineup>();

    [InverseProperty("CaptainAccount")]
    public virtual ICollection<Team> Team { get; set; } = new List<Team>();

    [InverseProperty("Account")]
    public virtual ICollection<TeamMember> TeamMember { get; set; } = new List<TeamMember>();

    [InverseProperty("BracketLockedByAccount")]
    public virtual ICollection<Tournament> TournamentBracketLockedByAccount { get; set; } = new List<Tournament>();

    [InverseProperty("Account")]
    public virtual ICollection<TournamentFollow> TournamentFollow { get; set; } = new List<TournamentFollow>();

    [InverseProperty("OrganizerAccount")]
    public virtual ICollection<Tournament> TournamentOrganizerAccount { get; set; } = new List<Tournament>();

    [InverseProperty("CreatedByAccount")]
    public virtual ICollection<TournamentSponsor> TournamentSponsor { get; set; } = new List<TournamentSponsor>();
}
