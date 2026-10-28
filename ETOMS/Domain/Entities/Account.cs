using System;
using System.Collections.Generic;

namespace ETOMS.Domain.Entities;

public partial class Account
{
    public int AccountId { get; set; }

    public string Email { get; set; } = null!;

    public string? PasswordHash { get; set; }

    public string? GoogleSubjectId { get; set; }

    public string FullName { get; set; } = null!;

    public string? AvatarUrl { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    public string? Phone { get; set; }

    public string? School { get; set; }

    public string? SocialLinks { get; set; }

    public string Role { get; set; } = null!;

    public string Status { get; set; } = null!;

    public string? BanReason { get; set; }

    public bool NotifyInApp { get; set; }

    public bool NotifyEmail { get; set; }

    public DateTime? EmailVerifiedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();

    public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();

    public virtual ICollection<AuthToken> AuthTokens { get; set; } = new List<AuthToken>();

    public virtual ICollection<Dispute> DisputeFlaggedByAccounts { get; set; } = new List<Dispute>();

    public virtual ICollection<Dispute> DisputeResolvedByAccounts { get; set; } = new List<Dispute>();

    public virtual ICollection<Livestream> Livestreams { get; set; } = new List<Livestream>();

    public virtual ICollection<MatchChatMessage> MatchChatMessageReportedByAccounts { get; set; } = new List<MatchChatMessage>();

    public virtual ICollection<MatchChatMessage> MatchChatMessageSenderAccounts { get; set; } = new List<MatchChatMessage>();

    public virtual ICollection<MatchGame> MatchGames { get; set; } = new List<MatchGame>();

    public virtual ICollection<MatchReport> MatchReports { get; set; } = new List<MatchReport>();

    public virtual ICollection<MatchRequest> MatchRequestDecidedByAccounts { get; set; } = new List<MatchRequest>();

    public virtual ICollection<MatchRequest> MatchRequestInAccounts { get; set; } = new List<MatchRequest>();

    public virtual ICollection<MatchRequest> MatchRequestOutAccounts { get; set; } = new List<MatchRequest>();

    public virtual ICollection<MatchRequest> MatchRequestRequestedByAccounts { get; set; } = new List<MatchRequest>();

    public virtual ICollection<Match> Matches { get; set; } = new List<Match>();

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    public virtual ICollection<PaymentRecord> PaymentRecords { get; set; } = new List<PaymentRecord>();

    public virtual ICollection<Penalty> Penalties { get; set; } = new List<Penalty>();

    public virtual ICollection<PlayerCredential> PlayerCredentials { get; set; } = new List<PlayerCredential>();

    public virtual ICollection<RecruitmentPost> RecruitmentPosts { get; set; } = new List<RecruitmentPost>();

    public virtual ICollection<RegistrationLineup> RegistrationLineups { get; set; } = new List<RegistrationLineup>();

    public virtual ICollection<Registration> Registrations { get; set; } = new List<Registration>();

    public virtual ICollection<TeamMember> TeamMembers { get; set; } = new List<TeamMember>();

    public virtual ICollection<Team> Teams { get; set; } = new List<Team>();

    public virtual ICollection<TournamentFollow> TournamentFollows { get; set; } = new List<TournamentFollow>();

    public virtual ICollection<TournamentSponsor> TournamentSponsors { get; set; } = new List<TournamentSponsor>();

    public virtual ICollection<Tournament> Tournaments { get; set; } = new List<Tournament>();
}
