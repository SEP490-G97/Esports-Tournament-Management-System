using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.HungModels;

public partial class EtomsDbContext : DbContext
{
    public EtomsDbContext(DbContextOptions<EtomsDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Account> Account { get; set; }

    public virtual DbSet<Attachment> Attachment { get; set; }

    public virtual DbSet<AuditLog> AuditLog { get; set; }

    public virtual DbSet<AuthToken> AuthToken { get; set; }

    public virtual DbSet<BracketNode> BracketNode { get; set; }

    public virtual DbSet<ContentItem> ContentItem { get; set; }

    public virtual DbSet<Dispute> Dispute { get; set; }

    public virtual DbSet<Game> Game { get; set; }

    public virtual DbSet<Livestream> Livestream { get; set; }

    public virtual DbSet<Match> Match { get; set; }

    public virtual DbSet<MatchChatMessage> MatchChatMessage { get; set; }

    public virtual DbSet<MatchGame> MatchGame { get; set; }

    public virtual DbSet<MatchReport> MatchReport { get; set; }

    public virtual DbSet<MatchRequest> MatchRequest { get; set; }

    public virtual DbSet<Notification> Notification { get; set; }

    public virtual DbSet<Organization> Organization { get; set; }

    public virtual DbSet<OrganizationDocument> OrganizationDocument { get; set; }

    public virtual DbSet<OrganizationMember> OrganizationMember { get; set; }

    public virtual DbSet<PaymentRecord> PaymentRecord { get; set; }

    public virtual DbSet<Penalty> Penalty { get; set; }

    public virtual DbSet<PlayerCredential> PlayerCredential { get; set; }

    public virtual DbSet<PromotionLink> PromotionLink { get; set; }

    public virtual DbSet<RecruitmentPost> RecruitmentPost { get; set; }

    public virtual DbSet<Registration> Registration { get; set; }

    public virtual DbSet<RegistrationLineup> RegistrationLineup { get; set; }

    public virtual DbSet<Team> Team { get; set; }

    public virtual DbSet<TeamMember> TeamMember { get; set; }

    public virtual DbSet<Tournament> Tournament { get; set; }

    public virtual DbSet<TournamentFollow> TournamentFollow { get; set; }

    public virtual DbSet<TournamentSponsor> TournamentSponsor { get; set; }

    public virtual DbSet<vw_GroupStanding> vw_GroupStanding { get; set; }

    public virtual DbSet<vw_Leaderboard> vw_Leaderboard { get; set; }

    public virtual DbSet<vw_TeamPenaltyPoint> vw_TeamPenaltyPoint { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.UseCollation("Vietnamese_CI_AS");

        modelBuilder.Entity<Account>(entity =>
        {
            entity.HasIndex(e => e.GoogleSubjectId, "UX_Account_Google")
                .IsUnique()
                .HasFilter("([GoogleSubjectId] IS NOT NULL)");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_Account_At");
            entity.Property(e => e.NotifyEmail).HasDefaultValue(true, "DF_Account_NotiMail");
            entity.Property(e => e.NotifyInApp).HasDefaultValue(true, "DF_Account_NotiApp");
            entity.Property(e => e.OrganizerRequestStatus).HasDefaultValue("NONE", "DF_Account_OrgReq");
            entity.Property(e => e.Role).HasDefaultValue("PLAYER", "DF_Account_Role");
            entity.Property(e => e.Status).HasDefaultValue("PENDING_VERIFY", "DF_Account_Status");
        });

        modelBuilder.Entity<Attachment>(entity =>
        {
            entity.Property(e => e.UploadedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_Attachment_At");

            entity.HasOne(d => d.UploadedByAccount).WithMany(p => p.Attachment)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Attachment_By");
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_AuditLog_At");

            entity.HasOne(d => d.ActorAccount).WithMany(p => p.AuditLog).HasConstraintName("FK_AuditLog_Actor");

            entity.HasOne(d => d.Tournament).WithMany(p => p.AuditLog).HasConstraintName("FK_AuditLog_Tour");
        });

        modelBuilder.Entity<AuthToken>(entity =>
        {
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_AuthToken_At");

            entity.HasOne(d => d.Account).WithMany(p => p.AuthToken)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AuthToken_Account");
        });

        modelBuilder.Entity<BracketNode>(entity =>
        {
            entity.Property(e => e.SeedingMethod).HasDefaultValue("MANUAL", "DF_BrNode_Seeding");
            entity.Property(e => e.StageNo).HasDefaultValue(1, "DF_BrNode_Stage");
            entity.Property(e => e.Status).HasDefaultValue("DRAFT", "DF_BrNode_Status");

            entity.HasOne(d => d.ParentNode).WithMany(p => p.InverseParentNode).HasConstraintName("FK_BracketNode_Parent");

            entity.HasOne(d => d.SeedTeam).WithMany(p => p.BracketNode).HasConstraintName("FK_BracketNode_Seed");

            entity.HasOne(d => d.Tournament).WithMany(p => p.BracketNode)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_BracketNode_Tour");
        });

        modelBuilder.Entity<ContentItem>(entity =>
        {
            entity.HasIndex(e => e.IsPinned, "UX_ContentItem_Pinned")
                .IsUnique()
                .HasFilter("([IsPinned]=(1))");

            entity.HasIndex(e => e.NotificationType, "UX_ContentItem_Template")
                .IsUnique()
                .HasFilter("([Kind]='TEMPLATE')");

            entity.Property(e => e.Status).HasDefaultValue("DRAFT", "DF_ContentItem_Status");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_ContentItem_At");

            entity.HasOne(d => d.AuthorAccount).WithMany(p => p.ContentItem)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ContentItem_Author");

            entity.HasOne(d => d.Tournament).WithMany(p => p.ContentItem).HasConstraintName("FK_ContentItem_Tour");
        });

        modelBuilder.Entity<Dispute>(entity =>
        {
            entity.HasIndex(e => e.MatchID, "UX_Dispute_LivePerMatch")
                .IsUnique()
                .HasFilter("([Status]<>'CLOSED' AND [Status]<>'RESOLVED')");

            entity.Property(e => e.AppealStatus).HasDefaultValue("NONE", "DF_Dispute_AppealS");
            entity.Property(e => e.OpenedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_Dispute_At");
            entity.Property(e => e.Status).HasDefaultValue("OPEN", "DF_Dispute_Status");

            entity.HasOne(d => d.FlaggedByAccount).WithMany(p => p.DisputeFlaggedByAccount).HasConstraintName("FK_Dispute_Flagger");

            entity.HasOne(d => d.Match).WithOne(p => p.Dispute)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Dispute_Match");

            entity.HasOne(d => d.ResolvedByAccount).WithMany(p => p.DisputeResolvedByAccount).HasConstraintName("FK_Dispute_Resolver");
        });

        modelBuilder.Entity<Game>(entity =>
        {
            entity.Property(e => e.MaxEvidenceFileSizeMB).HasDefaultValue(10, "DF_Game_MaxSize");
            entity.Property(e => e.Status).HasDefaultValue("ACTIVE", "DF_Game_Status");
        });

        modelBuilder.Entity<Livestream>(entity =>
        {
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_Livestream_At");
            entity.Property(e => e.Status).HasDefaultValue("SCHEDULED", "DF_Livestream_Status");

            entity.HasOne(d => d.CreatedByAccount).WithMany(p => p.Livestream)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Livestream_By");

            entity.HasOne(d => d.Match).WithMany(p => p.Livestream).HasConstraintName("FK_Livestream_Match");

            entity.HasOne(d => d.Tournament).WithMany(p => p.Livestream)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Livestream_Tour");
        });

        modelBuilder.Entity<Match>(entity =>
        {
            entity.Property(e => e.BestOf).HasDefaultValue(1, "DF_Match_BestOf");
            entity.Property(e => e.CheckInMode).HasDefaultValue("SELF", "DF_Match_CiMode");
            entity.Property(e => e.Status).HasDefaultValue("PENDING", "DF_Match_Status");

            entity.HasOne(d => d.BracketNode).WithMany(p => p.Match)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Match_Node");

            entity.HasOne(d => d.RefereeAccount).WithMany(p => p.Match).HasConstraintName("FK_Match_Referee");

            entity.HasOne(d => d.TeamA).WithMany(p => p.MatchTeamA).HasConstraintName("FK_Match_TeamA");

            entity.HasOne(d => d.TeamB).WithMany(p => p.MatchTeamB).HasConstraintName("FK_Match_TeamB");

            entity.HasOne(d => d.Tournament).WithMany(p => p.Match)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Match_Tour");

            entity.HasOne(d => d.WinnerTeam).WithMany(p => p.MatchWinnerTeam).HasConstraintName("FK_Match_Winner");
        });

        modelBuilder.Entity<MatchChatMessage>(entity =>
        {
            entity.Property(e => e.ReportStatus).HasDefaultValue("NONE", "DF_MatchChat_Rep");
            entity.Property(e => e.SentAt).HasDefaultValueSql("(sysutcdatetime())", "DF_MatchChat_At");

            entity.HasOne(d => d.Match).WithMany(p => p.MatchChatMessage)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MatchChatMessage_Match");

            entity.HasOne(d => d.ReportedByAccount).WithMany(p => p.MatchChatMessageReportedByAccount).HasConstraintName("FK_MatchChatMessage_Rep");

            entity.HasOne(d => d.SenderAccount).WithMany(p => p.MatchChatMessageSenderAccount)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MatchChatMessage_Sender");
        });

        modelBuilder.Entity<MatchGame>(entity =>
        {
            entity.HasOne(d => d.Match).WithMany(p => p.MatchGame)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MatchGame_Match");

            entity.HasOne(d => d.UpdatedByAccount).WithMany(p => p.MatchGame).HasConstraintName("FK_MatchGame_By");

            entity.HasOne(d => d.WinnerTeam).WithMany(p => p.MatchGame).HasConstraintName("FK_MatchGame_Winner");
        });

        modelBuilder.Entity<MatchReport>(entity =>
        {
            entity.Property(e => e.Status).HasDefaultValue("SUBMITTED", "DF_MatchReport_Status");
            entity.Property(e => e.SubmittedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_MatchReport_At");

            entity.HasOne(d => d.Match).WithMany(p => p.MatchReport)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MatchReport_Match");

            entity.HasOne(d => d.ReportedByAccount).WithMany(p => p.MatchReport)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MatchReport_By");

            entity.HasOne(d => d.Team).WithMany(p => p.MatchReport)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MatchReport_Team");
        });

        modelBuilder.Entity<MatchRequest>(entity =>
        {
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_MatchReq_At");
            entity.Property(e => e.Status).HasDefaultValue("PENDING", "DF_MatchReq_Status");

            entity.HasOne(d => d.DecidedByAccount).WithMany(p => p.MatchRequestDecidedByAccount).HasConstraintName("FK_MatchRequest_Dec");

            entity.HasOne(d => d.InAccount).WithMany(p => p.MatchRequestInAccount).HasConstraintName("FK_MatchRequest_In");

            entity.HasOne(d => d.Match).WithMany(p => p.MatchRequest)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MatchRequest_Match");

            entity.HasOne(d => d.OutAccount).WithMany(p => p.MatchRequestOutAccount).HasConstraintName("FK_MatchRequest_Out");

            entity.HasOne(d => d.RequestedByAccount).WithMany(p => p.MatchRequestRequestedByAccount)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MatchRequest_By");

            entity.HasOne(d => d.Team).WithMany(p => p.MatchRequest).HasConstraintName("FK_MatchRequest_Team");
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.Property(e => e.Channel).HasDefaultValue("IN_APP", "DF_Notification_Ch");
            entity.Property(e => e.SentAt).HasDefaultValueSql("(sysutcdatetime())", "DF_Notification_At");

            entity.HasOne(d => d.Account).WithMany(p => p.Notification)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Notification_Account");
        });

        modelBuilder.Entity<Organization>(entity =>
        {
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_Organization_At");
            entity.Property(e => e.Status).HasDefaultValue("PENDING", "DF_Organization_Status");

            entity.HasOne(d => d.CreatedByAccount).WithMany(p => p.OrganizationCreatedByAccount)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Organization_Creator");

            entity.HasOne(d => d.ReviewedByAccount).WithMany(p => p.OrganizationReviewedByAccount).HasConstraintName("FK_Organization_Reviewer");
        });

        modelBuilder.Entity<OrganizationDocument>(entity =>
        {
            entity.Property(e => e.UploadedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_OrgDocument_At");

            entity.HasOne(d => d.Organization).WithMany(p => p.OrganizationDocument)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OrganizationDocument_Org");
        });

        modelBuilder.Entity<OrganizationMember>(entity =>
        {
            entity.HasIndex(e => new { e.OrganizationID, e.Role }, "UX_OrganizationMember_Chair")
                .IsUnique()
                .HasFilter("([Role]='CHAIR' AND [Status]='ACTIVE')");

            entity.HasIndex(e => new { e.OrganizationID, e.AccountID }, "UX_OrganizationMember_Current")
                .IsUnique()
                .HasFilter("([Status]<>'REMOVED')");

            entity.Property(e => e.InvitedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_OrgMember_InvitedAt");
            entity.Property(e => e.Status).HasDefaultValue("INVITED", "DF_OrgMember_Status");

            entity.HasOne(d => d.Account).WithMany(p => p.OrganizationMember)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OrganizationMember_Account");

            entity.HasOne(d => d.Organization).WithMany(p => p.OrganizationMember)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OrganizationMember_Org");
        });

        modelBuilder.Entity<PaymentRecord>(entity =>
        {
            entity.HasIndex(e => e.RegistrationID, "UX_PaymentRecord_CurrentProof")
                .IsUnique()
                .HasFilter("([RecordKind]='PROOF' AND [IsCurrent]=(1))");

            entity.Property(e => e.IsCurrent).HasDefaultValue(true, "DF_PayRec_Current");
            entity.Property(e => e.RecordedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_PayRec_At");

            entity.HasOne(d => d.RecordedByAccount).WithMany(p => p.PaymentRecord)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PaymentRecord_By");

            entity.HasOne(d => d.Registration).WithOne(p => p.PaymentRecord)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PaymentRecord_Reg");
        });

        modelBuilder.Entity<Penalty>(entity =>
        {
            entity.HasIndex(e => e.CancelsPenaltyID, "UX_Penalty_CancelsOnce")
                .IsUnique()
                .HasFilter("([CancelsPenaltyID] IS NOT NULL)");

            entity.Property(e => e.IssuedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_Penalty_At");
            entity.Property(e => e.Points).HasDefaultValue(1, "DF_Penalty_Points");
            entity.Property(e => e.Status).HasDefaultValue("ACTIVE", "DF_Penalty_Status");

            entity.HasOne(d => d.CancelsPenalty).WithOne(p => p.InverseCancelsPenalty).HasConstraintName("FK_Penalty_Cancels");

            entity.HasOne(d => d.IssuedByAccount).WithMany(p => p.Penalty)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Penalty_By");

            entity.HasOne(d => d.Match).WithMany(p => p.Penalty).HasConstraintName("FK_Penalty_Match");

            entity.HasOne(d => d.Team).WithMany(p => p.Penalty)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Penalty_Team");

            entity.HasOne(d => d.Tournament).WithMany(p => p.Penalty)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Penalty_Tour");
        });

        modelBuilder.Entity<PlayerCredential>(entity =>
        {
            entity.HasIndex(e => new { e.AccountID, e.GameID }, "UX_PlayerCredential_Ign")
                .IsUnique()
                .HasFilter("([CredentialType]='IN_GAME_NAME')");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_PlayerCred_At");

            entity.HasOne(d => d.Account).WithMany(p => p.PlayerCredential)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PlayerCredential_Account");

            entity.HasOne(d => d.Game).WithMany(p => p.PlayerCredential).HasConstraintName("FK_PlayerCredential_Game");
        });

        modelBuilder.Entity<PromotionLink>(entity =>
        {
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_PromotionLink_At");

            entity.HasOne(d => d.CreatedByAccount).WithMany(p => p.PromotionLink)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PromotionLink_By");

            entity.HasOne(d => d.Tournament).WithMany(p => p.PromotionLink)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PromotionLink_Tour");
        });

        modelBuilder.Entity<RecruitmentPost>(entity =>
        {
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_RecPost_At");
            entity.Property(e => e.Status).HasDefaultValue("OPEN", "DF_RecPost_Status");

            entity.HasOne(d => d.AuthorAccount).WithMany(p => p.RecruitmentPost)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_RecruitmentPost_Author");

            entity.HasOne(d => d.Game).WithMany(p => p.RecruitmentPost)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_RecruitmentPost_Game");

            entity.HasOne(d => d.Team).WithMany(p => p.RecruitmentPost).HasConstraintName("FK_RecruitmentPost_Team");
        });

        modelBuilder.Entity<Registration>(entity =>
        {
            entity.HasIndex(e => new { e.TournamentID, e.TeamID }, "UX_Registration_OneLivePerTeam")
                .IsUnique()
                .HasFilter("([Status]<>'WITHDRAWN' AND [Status]<>'REJECTED')");

            entity.HasIndex(e => new { e.TournamentID, e.WaitlistPosition }, "UX_Registration_WaitPosition")
                .IsUnique()
                .HasFilter("([Status]='WAITLISTED')");

            entity.Property(e => e.FinalPlacement).HasDefaultValue("NONE", "DF_Reg_Place");
            entity.Property(e => e.PaymentStatus).HasDefaultValue("UNPAID", "DF_Reg_Pay");
            entity.Property(e => e.Status).HasDefaultValue("DRAFT", "DF_Reg_Status");

            entity.HasOne(d => d.SubmittedByAccount).WithMany(p => p.Registration)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Registration_By");

            entity.HasOne(d => d.Team).WithMany(p => p.Registration)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Registration_Team");

            entity.HasOne(d => d.Tournament).WithMany(p => p.Registration)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Registration_Tour");
        });

        modelBuilder.Entity<RegistrationLineup>(entity =>
        {
            entity.HasOne(d => d.Account).WithMany(p => p.RegistrationLineup)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_RegistrationLineup_Acc");

            entity.HasOne(d => d.PlayerCredential).WithMany(p => p.RegistrationLineup).HasConstraintName("FK_RegistrationLineup_Cred");

            entity.HasOne(d => d.Registration).WithMany(p => p.RegistrationLineup)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_RegistrationLineup_Reg");
        });

        modelBuilder.Entity<Team>(entity =>
        {
            entity.HasIndex(e => e.InviteCode, "UX_Team_InviteCode")
                .IsUnique()
                .HasFilter("([InviteCode] IS NOT NULL)");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_Team_At");
            entity.Property(e => e.Status).HasDefaultValue("ACTIVE", "DF_Team_Status");

            entity.HasOne(d => d.CaptainAccount).WithMany(p => p.Team)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Team_Captain");

            entity.HasOne(d => d.Game).WithMany(p => p.Team)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Team_Game");
        });

        modelBuilder.Entity<TeamMember>(entity =>
        {
            entity.HasIndex(e => new { e.TeamID, e.AccountID }, "UX_TeamMember_ActiveInTeam")
                .IsUnique()
                .HasFilter("([Status]='ACTIVE')");

            entity.HasIndex(e => new { e.AccountID, e.GameID }, "UX_TeamMember_OneTeamPerGame")
                .IsUnique()
                .HasFilter("([Status]='ACTIVE')");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_TeamMember_At");
            entity.Property(e => e.Slot).HasDefaultValue("STARTER", "DF_TeamMember_Slot");
            entity.Property(e => e.Status).HasDefaultValue("PENDING", "DF_TeamMember_Status");

            entity.HasOne(d => d.Account).WithMany(p => p.TeamMember)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TeamMember_Account");

            entity.HasOne(d => d.Game).WithMany(p => p.TeamMember)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TeamMember_Game");

            entity.HasOne(d => d.RecruitmentPost).WithMany(p => p.TeamMember).HasConstraintName("FK_TeamMember_Post");

            entity.HasOne(d => d.Team).WithMany(p => p.TeamMember)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TeamMember_Team");
        });

        modelBuilder.Entity<Tournament>(entity =>
        {
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_Tour_At");
            entity.Property(e => e.IsOnline).HasDefaultValue(true, "DF_Tour_Online");
            entity.Property(e => e.RequireIdentityDocument).HasDefaultValue(true, "DF_Tour_ReqDoc");
            entity.Property(e => e.RequireInGameName).HasDefaultValue(true, "DF_Tour_ReqIgn");
            entity.Property(e => e.RequirePaymentProof).HasDefaultValue(true, "DF_Tour_ReqPay");
            entity.Property(e => e.RulebookVersion).HasDefaultValue(1, "DF_Tour_RbVer");
            entity.Property(e => e.Status).HasDefaultValue("DRAFT", "DF_Tour_Status");

            entity.HasOne(d => d.BracketLockedByAccount).WithMany(p => p.TournamentBracketLockedByAccount).HasConstraintName("FK_Tournament_LockedBy");

            entity.HasOne(d => d.Game).WithMany(p => p.Tournament)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Tournament_Game");

            entity.HasOne(d => d.Organization).WithMany(p => p.Tournament).HasConstraintName("FK_Tournament_Organization");

            entity.HasOne(d => d.OrganizerAccount).WithMany(p => p.TournamentOrganizerAccount)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Tournament_Organizer");
        });

        modelBuilder.Entity<TournamentFollow>(entity =>
        {
            entity.Property(e => e.FollowedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_TourFollow_At");

            entity.HasOne(d => d.Account).WithMany(p => p.TournamentFollow)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TournamentFollow_Acc");

            entity.HasOne(d => d.Tournament).WithMany(p => p.TournamentFollow)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TournamentFollow_Tour");
        });

        modelBuilder.Entity<TournamentSponsor>(entity =>
        {
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_TourSponsor_At");
            entity.Property(e => e.DisplayOrder).HasDefaultValue(1, "DF_TourSponsor_Order");
            entity.Property(e => e.ShowWhenCancelled).HasDefaultValue(true, "DF_TourSponsor_Show");

            entity.HasOne(d => d.CreatedByAccount).WithMany(p => p.TournamentSponsor)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TournamentSponsor_By");

            entity.HasOne(d => d.Tournament).WithMany(p => p.TournamentSponsor)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TournamentSponsor_Tour");
        });

        modelBuilder.Entity<vw_GroupStanding>(entity =>
        {
            entity.ToView("vw_GroupStanding");
        });

        modelBuilder.Entity<vw_Leaderboard>(entity =>
        {
            entity.ToView("vw_Leaderboard");
        });

        modelBuilder.Entity<vw_TeamPenaltyPoint>(entity =>
        {
            entity.ToView("vw_TeamPenaltyPoint");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
