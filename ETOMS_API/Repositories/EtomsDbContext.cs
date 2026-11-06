using System;
using System.Collections.Generic;
using ETOMS_API.Models;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Repositories;

public partial class EtomsDbContext : DbContext
{
    public EtomsDbContext(DbContextOptions<EtomsDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Account> Accounts { get; set; }

    public virtual DbSet<Attachment> Attachments { get; set; }

    public virtual DbSet<AuditLog> AuditLogs { get; set; }

    public virtual DbSet<AuthToken> AuthTokens { get; set; }

    public virtual DbSet<BracketNode> BracketNodes { get; set; }

    public virtual DbSet<ContentItem> ContentItems { get; set; }

    public virtual DbSet<Dispute> Disputes { get; set; }

    public virtual DbSet<Game> Games { get; set; }

    public virtual DbSet<Livestream> Livestreams { get; set; }

    public virtual DbSet<Match> Matches { get; set; }

    public virtual DbSet<MatchChatMessage> MatchChatMessages { get; set; }

    public virtual DbSet<MatchGame> MatchGames { get; set; }

    public virtual DbSet<MatchReport> MatchReports { get; set; }

    public virtual DbSet<MatchRequest> MatchRequests { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<Organization> Organizations { get; set; }

    public virtual DbSet<OrganizationDocument> OrganizationDocuments { get; set; }

    public virtual DbSet<OrganizationMember> OrganizationMembers { get; set; }

    public virtual DbSet<PaymentRecord> PaymentRecords { get; set; }

    public virtual DbSet<Penalty> Penalties { get; set; }

    public virtual DbSet<PlayerCredential> PlayerCredentials { get; set; }

    public virtual DbSet<PromotionLink> PromotionLinks { get; set; }

    public virtual DbSet<RecruitmentPost> RecruitmentPosts { get; set; }

    public virtual DbSet<Registration> Registrations { get; set; }

    public virtual DbSet<RegistrationLineup> RegistrationLineups { get; set; }

    public virtual DbSet<Team> Teams { get; set; }

    public virtual DbSet<TeamMember> TeamMembers { get; set; }

    public virtual DbSet<Tournament> Tournaments { get; set; }

    public virtual DbSet<TournamentFollow> TournamentFollows { get; set; }

    public virtual DbSet<TournamentSponsor> TournamentSponsors { get; set; }

    public virtual DbSet<VwGroupStanding> VwGroupStandings { get; set; }

    public virtual DbSet<VwLeaderboard> VwLeaderboards { get; set; }

    public virtual DbSet<VwOrganizationReputation> VwOrganizationReputations { get; set; }

    public virtual DbSet<VwTeamPenaltyPoint> VwTeamPenaltyPoints { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasCharSet("utf8mb4");

        modelBuilder.Entity<Account>(entity =>
        {
            entity.HasKey(e => e.AccountId).HasName("PRIMARY");

            entity
                .ToTable("Account")
                .UseCollation("utf8mb4_0900_as_ci");

            entity.HasIndex(e => e.Email, "UQ_Account_Email").IsUnique();

            entity.HasIndex(e => e.GoogleSubjectId, "UX_Account_Google").IsUnique();

            entity.Property(e => e.AccountId).HasColumnName("AccountID");
            entity.Property(e => e.AvatarUrl).HasMaxLength(500);
            entity.Property(e => e.BanReason).HasMaxLength(500);
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("utc_timestamp()")
                .HasColumnType("datetime");
            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property(e => e.EmailVerifiedAt).HasColumnType("datetime");
            entity.Property(e => e.FullName).HasMaxLength(150);
            entity.Property(e => e.GoogleSubjectId).HasMaxLength(100);
            entity.Property(e => e.NotifyEmail)
                .IsRequired()
                .HasDefaultValueSql("'1'");
            entity.Property(e => e.NotifyInApp)
                .IsRequired()
                .HasDefaultValueSql("'1'");
            entity.Property(e => e.OrganizerRequestStatus)
                .HasMaxLength(20)
                .HasDefaultValueSql("'NONE'");
            entity.Property(e => e.OrganizerRequestedAt).HasColumnType("datetime");
            entity.Property(e => e.PasswordHash).HasMaxLength(255);
            entity.Property(e => e.Phone).HasMaxLength(20);
            entity.Property(e => e.Role)
                .HasMaxLength(20)
                .HasDefaultValueSql("'PLAYER'");
            entity.Property(e => e.SocialLinks).HasMaxLength(500);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValueSql("'PENDING_VERIFY'");
        });

        modelBuilder.Entity<Attachment>(entity =>
        {
            entity.HasKey(e => e.AttachmentId).HasName("PRIMARY");

            entity
                .ToTable("Attachment")
                .UseCollation("utf8mb4_0900_as_ci");

            entity.HasIndex(e => e.UploadedByAccountId, "FK_Attachment_By");

            entity.HasIndex(e => new { e.OwnerType, e.OwnerId }, "IX_Attachment_Owner");

            entity.Property(e => e.AttachmentId).HasColumnName("AttachmentID");
            entity.Property(e => e.FileType).HasMaxLength(20);
            entity.Property(e => e.FileUrl).HasMaxLength(500);
            entity.Property(e => e.OwnerId).HasColumnName("OwnerID");
            entity.Property(e => e.OwnerType).HasMaxLength(20);
            entity.Property(e => e.UploadedAt)
                .HasDefaultValueSql("utc_timestamp()")
                .HasColumnType("datetime");
            entity.Property(e => e.UploadedByAccountId).HasColumnName("UploadedByAccountID");

            entity.HasOne(d => d.UploadedByAccount).WithMany(p => p.Attachments)
                .HasForeignKey(d => d.UploadedByAccountId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Attachment_By");
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.AuditLogId).HasName("PRIMARY");

            entity
                .ToTable("AuditLog")
                .UseCollation("utf8mb4_0900_as_ci");

            entity.HasIndex(e => e.ActorAccountId, "FK_AuditLog_Actor");

            entity.HasIndex(e => new { e.TournamentId, e.CreatedAt }, "IX_AuditLog_Timeline");

            entity.Property(e => e.AuditLogId).HasColumnName("AuditLogID");
            entity.Property(e => e.ActionType).HasMaxLength(30);
            entity.Property(e => e.ActorAccountId).HasColumnName("ActorAccountID");
            entity.Property(e => e.AfterValue).HasColumnType("mediumtext");
            entity.Property(e => e.BeforeValue).HasColumnType("mediumtext");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("utc_timestamp()")
                .HasColumnType("datetime");
            entity.Property(e => e.ObjectId).HasColumnName("ObjectID");
            entity.Property(e => e.ObjectType).HasMaxLength(30);
            entity.Property(e => e.Reason).HasMaxLength(1000);
            entity.Property(e => e.TournamentId).HasColumnName("TournamentID");

            entity.HasOne(d => d.ActorAccount).WithMany(p => p.AuditLogs)
                .HasForeignKey(d => d.ActorAccountId)
                .HasConstraintName("FK_AuditLog_Actor");

            entity.HasOne(d => d.Tournament).WithMany(p => p.AuditLogs)
                .HasForeignKey(d => d.TournamentId)
                .HasConstraintName("FK_AuditLog_Tour");
        });

        modelBuilder.Entity<AuthToken>(entity =>
        {
            entity.HasKey(e => e.AuthTokenId).HasName("PRIMARY");

            entity
                .ToTable("AuthToken")
                .UseCollation("utf8mb4_0900_as_ci");

            entity.HasIndex(e => e.AccountId, "FK_AuthToken_Account");

            entity.HasIndex(e => e.TokenHash, "UQ_AuthToken_Hash").IsUnique();

            entity.Property(e => e.AuthTokenId).HasColumnName("AuthTokenID");
            entity.Property(e => e.AccountId).HasColumnName("AccountID");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("utc_timestamp()")
                .HasColumnType("datetime");
            entity.Property(e => e.ExpiresAt).HasColumnType("datetime");
            entity.Property(e => e.TokenType).HasMaxLength(20);
            entity.Property(e => e.UsedAt).HasColumnType("datetime");

            entity.HasOne(d => d.Account).WithMany(p => p.AuthTokens)
                .HasForeignKey(d => d.AccountId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AuthToken_Account");
        });

        modelBuilder.Entity<BracketNode>(entity =>
        {
            entity.HasKey(e => e.BracketNodeId).HasName("PRIMARY");

            entity
                .ToTable("BracketNode")
                .UseCollation("utf8mb4_0900_as_ci");

            entity.HasIndex(e => e.SeedTeamId, "FK_BracketNode_Seed");

            entity.HasIndex(e => e.ParentNodeId, "IX_BracketNode_Parent");

            entity.HasIndex(e => new { e.TournamentId, e.StageNo, e.RoundNo }, "IX_BracketNode_Tour");

            entity.Property(e => e.BracketNodeId).HasColumnName("BracketNodeID");
            entity.Property(e => e.GeneratedAt).HasColumnType("datetime");
            entity.Property(e => e.GroupLabel).HasMaxLength(10);
            entity.Property(e => e.NodeType).HasMaxLength(20);
            entity.Property(e => e.ParentNodeId).HasColumnName("ParentNodeID");
            entity.Property(e => e.SeedTeamId).HasColumnName("SeedTeamID");
            entity.Property(e => e.SeedingMethod)
                .HasMaxLength(20)
                .HasDefaultValueSql("'MANUAL'");
            entity.Property(e => e.StageNo).HasDefaultValueSql("'1'");
            entity.Property(e => e.StageType).HasMaxLength(20);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValueSql("'DRAFT'");
            entity.Property(e => e.TournamentId).HasColumnName("TournamentID");

            entity.HasOne(d => d.ParentNode).WithMany(p => p.InverseParentNode)
                .HasForeignKey(d => d.ParentNodeId)
                .HasConstraintName("FK_BracketNode_Parent");

            entity.HasOne(d => d.SeedTeam).WithMany(p => p.BracketNodes)
                .HasForeignKey(d => d.SeedTeamId)
                .HasConstraintName("FK_BracketNode_Seed");

            entity.HasOne(d => d.Tournament).WithMany(p => p.BracketNodes)
                .HasForeignKey(d => d.TournamentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_BracketNode_Tour");
        });

        modelBuilder.Entity<ContentItem>(entity =>
        {
            entity.HasKey(e => e.ContentItemId).HasName("PRIMARY");

            entity
                .ToTable("ContentItem")
                .UseCollation("utf8mb4_0900_as_ci");

            entity.HasIndex(e => e.AuthorAccountId, "FK_ContentItem_Author");

            entity.HasIndex(e => new { e.TournamentId, e.Status }, "IX_ContentItem_Tour");

            entity.Property(e => e.ContentItemId).HasColumnName("ContentItemID");
            entity.Property(e => e.AuthorAccountId).HasColumnName("AuthorAccountID");
            entity.Property(e => e.Body).HasColumnType("mediumtext");
            entity.Property(e => e.Kind).HasMaxLength(20);
            entity.Property(e => e.NotificationType).HasMaxLength(30);
            entity.Property(e => e.PublishedAt).HasColumnType("datetime");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValueSql("'DRAFT'");
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.TournamentId).HasColumnName("TournamentID");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("utc_timestamp()")
                .HasColumnType("datetime");

            entity.HasOne(d => d.AuthorAccount).WithMany(p => p.ContentItems)
                .HasForeignKey(d => d.AuthorAccountId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ContentItem_Author");

            entity.HasOne(d => d.Tournament).WithMany(p => p.ContentItems)
                .HasForeignKey(d => d.TournamentId)
                .HasConstraintName("FK_ContentItem_Tour");
        });

        modelBuilder.Entity<Dispute>(entity =>
        {
            entity.HasKey(e => e.DisputeId).HasName("PRIMARY");

            entity
                .ToTable("Dispute")
                .UseCollation("utf8mb4_0900_as_ci");

            entity.HasIndex(e => e.FlaggedByAccountId, "FK_Dispute_Flagger");

            entity.HasIndex(e => e.MatchId, "FK_Dispute_Match");

            entity.HasIndex(e => e.ResolvedByAccountId, "FK_Dispute_Resolver");

            entity.Property(e => e.DisputeId).HasColumnName("DisputeID");
            entity.Property(e => e.AppealOutcomeNote).HasMaxLength(1000);
            entity.Property(e => e.AppealReason).HasMaxLength(1000);
            entity.Property(e => e.AppealStatus)
                .HasMaxLength(20)
                .HasDefaultValueSql("'NONE'");
            entity.Property(e => e.FlagCategory).HasMaxLength(30);
            entity.Property(e => e.FlagNote).HasMaxLength(1000);
            entity.Property(e => e.FlaggedByAccountId).HasColumnName("FlaggedByAccountID");
            entity.Property(e => e.MatchId).HasColumnName("MatchID");
            entity.Property(e => e.OpenReason).HasMaxLength(30);
            entity.Property(e => e.OpenedAt)
                .HasDefaultValueSql("utc_timestamp()")
                .HasColumnType("datetime");
            entity.Property(e => e.ResolvedAt).HasColumnType("datetime");
            entity.Property(e => e.ResolvedByAccountId).HasColumnName("ResolvedByAccountID");
            entity.Property(e => e.RulingReason).HasMaxLength(1000);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValueSql("'OPEN'");

            entity.HasOne(d => d.FlaggedByAccount).WithMany(p => p.DisputeFlaggedByAccounts)
                .HasForeignKey(d => d.FlaggedByAccountId)
                .HasConstraintName("FK_Dispute_Flagger");

            entity.HasOne(d => d.Match).WithMany(p => p.Disputes)
                .HasForeignKey(d => d.MatchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Dispute_Match");

            entity.HasOne(d => d.ResolvedByAccount).WithMany(p => p.DisputeResolvedByAccounts)
                .HasForeignKey(d => d.ResolvedByAccountId)
                .HasConstraintName("FK_Dispute_Resolver");
        });

        modelBuilder.Entity<Game>(entity =>
        {
            entity.HasKey(e => e.GameId).HasName("PRIMARY");

            entity
                .ToTable("Game")
                .UseCollation("utf8mb4_0900_as_ci");

            entity.HasIndex(e => e.ShortCode, "UQ_Game_Code").IsUnique();

            entity.HasIndex(e => e.Title, "UQ_Game_Title").IsUnique();

            entity.Property(e => e.GameId).HasColumnName("GameID");
            entity.Property(e => e.AllowedEvidenceTypes).HasMaxLength(200);
            entity.Property(e => e.BannerUrl).HasMaxLength(500);
            entity.Property(e => e.DefaultFormat).HasMaxLength(30);
            entity.Property(e => e.LogoUrl).HasMaxLength(500);
            entity.Property(e => e.MaxEvidenceFileSizeMb)
                .HasDefaultValueSql("'10'")
                .HasColumnName("MaxEvidenceFileSizeMB");
            entity.Property(e => e.ShortCode).HasMaxLength(10);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValueSql("'ACTIVE'");
            entity.Property(e => e.Title).HasMaxLength(150);
        });

        modelBuilder.Entity<Livestream>(entity =>
        {
            entity.HasKey(e => e.LivestreamId).HasName("PRIMARY");

            entity
                .ToTable("Livestream")
                .UseCollation("utf8mb4_0900_as_ci");

            entity.HasIndex(e => e.CreatedByAccountId, "FK_Livestream_By");

            entity.HasIndex(e => e.MatchId, "FK_Livestream_Match");

            entity.HasIndex(e => new { e.TournamentId, e.Status }, "IX_Livestream_Tour");

            entity.Property(e => e.LivestreamId).HasColumnName("LivestreamID");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("utc_timestamp()")
                .HasColumnType("datetime");
            entity.Property(e => e.CreatedByAccountId).HasColumnName("CreatedByAccountID");
            entity.Property(e => e.MatchId).HasColumnName("MatchID");
            entity.Property(e => e.Platform).HasMaxLength(20);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValueSql("'SCHEDULED'");
            entity.Property(e => e.StreamUrl).HasMaxLength(500);
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.TournamentId).HasColumnName("TournamentID");

            entity.HasOne(d => d.CreatedByAccount).WithMany(p => p.Livestreams)
                .HasForeignKey(d => d.CreatedByAccountId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Livestream_By");

            entity.HasOne(d => d.Match).WithMany(p => p.Livestreams)
                .HasForeignKey(d => d.MatchId)
                .HasConstraintName("FK_Livestream_Match");

            entity.HasOne(d => d.Tournament).WithMany(p => p.Livestreams)
                .HasForeignKey(d => d.TournamentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Livestream_Tour");
        });

        modelBuilder.Entity<Match>(entity =>
        {
            entity.HasKey(e => e.MatchId).HasName("PRIMARY");

            entity
                .ToTable("Match")
                .UseCollation("utf8mb4_0900_as_ci");

            entity.HasIndex(e => e.BracketNodeId, "FK_Match_Node");

            entity.HasIndex(e => e.RefereeAccountId, "FK_Match_Referee");

            entity.HasIndex(e => e.TeamAid, "FK_Match_TeamA");

            entity.HasIndex(e => e.TeamBid, "FK_Match_TeamB");

            entity.HasIndex(e => e.WinnerTeamId, "FK_Match_Winner");

            entity.HasIndex(e => e.ScheduledAt, "IX_Match_Schedule");

            entity.HasIndex(e => new { e.TournamentId, e.Status }, "IX_Match_Tour");

            entity.Property(e => e.MatchId).HasColumnName("MatchID");
            entity.Property(e => e.BestOf).HasDefaultValueSql("'1'");
            entity.Property(e => e.BracketNodeId).HasColumnName("BracketNodeID");
            entity.Property(e => e.CheckInMode)
                .HasMaxLength(20)
                .HasDefaultValueSql("'SELF'");
            entity.Property(e => e.CheckInOpenAt).HasColumnType("datetime");
            entity.Property(e => e.OverrideReason).HasMaxLength(500);
            entity.Property(e => e.RefereeAccountId).HasColumnName("RefereeAccountID");
            entity.Property(e => e.ReportDeadlineAt).HasColumnType("datetime");
            entity.Property(e => e.ResultSource).HasMaxLength(20);
            entity.Property(e => e.RoomCode).HasMaxLength(100);
            entity.Property(e => e.RoomPassword).HasMaxLength(100);
            entity.Property(e => e.ScheduledAt).HasColumnType("datetime");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValueSql("'PENDING'");
            entity.Property(e => e.TeamAcheckedInAt)
                .HasColumnType("datetime")
                .HasColumnName("TeamACheckedInAt");
            entity.Property(e => e.TeamAid).HasColumnName("TeamAID");
            entity.Property(e => e.TeamBcheckedInAt)
                .HasColumnType("datetime")
                .HasColumnName("TeamBCheckedInAt");
            entity.Property(e => e.TeamBid).HasColumnName("TeamBID");
            entity.Property(e => e.TournamentId).HasColumnName("TournamentID");
            entity.Property(e => e.VerifiedAt).HasColumnType("datetime");
            entity.Property(e => e.WinnerTeamId).HasColumnName("WinnerTeamID");

            entity.HasOne(d => d.BracketNode).WithMany(p => p.Matches)
                .HasForeignKey(d => d.BracketNodeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Match_Node");

            entity.HasOne(d => d.RefereeAccount).WithMany(p => p.Matches)
                .HasForeignKey(d => d.RefereeAccountId)
                .HasConstraintName("FK_Match_Referee");

            entity.HasOne(d => d.TeamA).WithMany(p => p.MatchTeamAs)
                .HasForeignKey(d => d.TeamAid)
                .HasConstraintName("FK_Match_TeamA");

            entity.HasOne(d => d.TeamB).WithMany(p => p.MatchTeamBs)
                .HasForeignKey(d => d.TeamBid)
                .HasConstraintName("FK_Match_TeamB");

            entity.HasOne(d => d.Tournament).WithMany(p => p.Matches)
                .HasForeignKey(d => d.TournamentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Match_Tour");

            entity.HasOne(d => d.WinnerTeam).WithMany(p => p.MatchWinnerTeams)
                .HasForeignKey(d => d.WinnerTeamId)
                .HasConstraintName("FK_Match_Winner");
        });

        modelBuilder.Entity<MatchChatMessage>(entity =>
        {
            entity.HasKey(e => e.MatchChatMessageId).HasName("PRIMARY");

            entity
                .ToTable("MatchChatMessage")
                .UseCollation("utf8mb4_0900_as_ci");

            entity.HasIndex(e => e.ReportedByAccountId, "FK_MatchChatMessage_Rep");

            entity.HasIndex(e => e.SenderAccountId, "FK_MatchChatMessage_Sender");

            entity.HasIndex(e => new { e.MatchId, e.SentAt }, "IX_MatchChatMessage_Match");

            entity.Property(e => e.MatchChatMessageId).HasColumnName("MatchChatMessageID");
            entity.Property(e => e.Content).HasMaxLength(2000);
            entity.Property(e => e.MatchId).HasColumnName("MatchID");
            entity.Property(e => e.ReportNote).HasMaxLength(500);
            entity.Property(e => e.ReportStatus)
                .HasMaxLength(20)
                .HasDefaultValueSql("'NONE'");
            entity.Property(e => e.ReportedByAccountId).HasColumnName("ReportedByAccountID");
            entity.Property(e => e.SenderAccountId).HasColumnName("SenderAccountID");
            entity.Property(e => e.SentAt)
                .HasDefaultValueSql("utc_timestamp()")
                .HasColumnType("datetime");

            entity.HasOne(d => d.Match).WithMany(p => p.MatchChatMessages)
                .HasForeignKey(d => d.MatchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MatchChatMessage_Match");

            entity.HasOne(d => d.ReportedByAccount).WithMany(p => p.MatchChatMessageReportedByAccounts)
                .HasForeignKey(d => d.ReportedByAccountId)
                .HasConstraintName("FK_MatchChatMessage_Rep");

            entity.HasOne(d => d.SenderAccount).WithMany(p => p.MatchChatMessageSenderAccounts)
                .HasForeignKey(d => d.SenderAccountId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MatchChatMessage_Sender");
        });

        modelBuilder.Entity<MatchGame>(entity =>
        {
            entity.HasKey(e => e.MatchGameId).HasName("PRIMARY");

            entity
                .ToTable("MatchGame")
                .UseCollation("utf8mb4_0900_as_ci");

            entity.HasIndex(e => e.UpdatedByAccountId, "FK_MatchGame_By");

            entity.HasIndex(e => e.WinnerTeamId, "FK_MatchGame_Winner");

            entity.HasIndex(e => new { e.MatchId, e.GameNo }, "UQ_MatchGame_Number").IsUnique();

            entity.Property(e => e.MatchGameId).HasColumnName("MatchGameID");
            entity.Property(e => e.MatchId).HasColumnName("MatchID");
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime");
            entity.Property(e => e.UpdatedByAccountId).HasColumnName("UpdatedByAccountID");
            entity.Property(e => e.WinnerTeamId).HasColumnName("WinnerTeamID");

            entity.HasOne(d => d.Match).WithMany(p => p.MatchGames)
                .HasForeignKey(d => d.MatchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MatchGame_Match");

            entity.HasOne(d => d.UpdatedByAccount).WithMany(p => p.MatchGames)
                .HasForeignKey(d => d.UpdatedByAccountId)
                .HasConstraintName("FK_MatchGame_By");

            entity.HasOne(d => d.WinnerTeam).WithMany(p => p.MatchGames)
                .HasForeignKey(d => d.WinnerTeamId)
                .HasConstraintName("FK_MatchGame_Winner");
        });

        modelBuilder.Entity<MatchReport>(entity =>
        {
            entity.HasKey(e => e.MatchReportId).HasName("PRIMARY");

            entity
                .ToTable("MatchReport")
                .UseCollation("utf8mb4_0900_as_ci");

            entity.HasIndex(e => e.ReportedByAccountId, "FK_MatchReport_By");

            entity.HasIndex(e => e.TeamId, "FK_MatchReport_Team");

            entity.HasIndex(e => new { e.MatchId, e.TeamId }, "UQ_MatchReport_TeamMatch").IsUnique();

            entity.Property(e => e.MatchReportId).HasColumnName("MatchReportID");
            entity.Property(e => e.MatchId).HasColumnName("MatchID");
            entity.Property(e => e.PerGameScores).HasMaxLength(200);
            entity.Property(e => e.ReportedByAccountId).HasColumnName("ReportedByAccountID");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValueSql("'SUBMITTED'");
            entity.Property(e => e.SubmittedAt)
                .HasDefaultValueSql("utc_timestamp()")
                .HasColumnType("datetime");
            entity.Property(e => e.TeamId).HasColumnName("TeamID");

            entity.HasOne(d => d.Match).WithMany(p => p.MatchReports)
                .HasForeignKey(d => d.MatchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MatchReport_Match");

            entity.HasOne(d => d.ReportedByAccount).WithMany(p => p.MatchReports)
                .HasForeignKey(d => d.ReportedByAccountId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MatchReport_By");

            entity.HasOne(d => d.Team).WithMany(p => p.MatchReports)
                .HasForeignKey(d => d.TeamId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MatchReport_Team");
        });

        modelBuilder.Entity<MatchRequest>(entity =>
        {
            entity.HasKey(e => e.MatchRequestId).HasName("PRIMARY");

            entity
                .ToTable("MatchRequest")
                .UseCollation("utf8mb4_0900_as_ci");

            entity.HasIndex(e => e.RequestedByAccountId, "FK_MatchRequest_By");

            entity.HasIndex(e => e.DecidedByAccountId, "FK_MatchRequest_Dec");

            entity.HasIndex(e => e.InAccountId, "FK_MatchRequest_In");

            entity.HasIndex(e => e.MatchId, "FK_MatchRequest_Match");

            entity.HasIndex(e => e.OutAccountId, "FK_MatchRequest_Out");

            entity.HasIndex(e => e.TeamId, "FK_MatchRequest_Team");

            entity.Property(e => e.MatchRequestId).HasColumnName("MatchRequestID");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("utc_timestamp()")
                .HasColumnType("datetime");
            entity.Property(e => e.DecidedAt).HasColumnType("datetime");
            entity.Property(e => e.DecidedByAccountId).HasColumnName("DecidedByAccountID");
            entity.Property(e => e.InAccountId).HasColumnName("InAccountID");
            entity.Property(e => e.MatchId).HasColumnName("MatchID");
            entity.Property(e => e.OutAccountId).HasColumnName("OutAccountID");
            entity.Property(e => e.Reason).HasMaxLength(500);
            entity.Property(e => e.RequestKind).HasMaxLength(20);
            entity.Property(e => e.RequestedByAccountId).HasColumnName("RequestedByAccountID");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValueSql("'PENDING'");
            entity.Property(e => e.TeamId).HasColumnName("TeamID");

            entity.HasOne(d => d.DecidedByAccount).WithMany(p => p.MatchRequestDecidedByAccounts)
                .HasForeignKey(d => d.DecidedByAccountId)
                .HasConstraintName("FK_MatchRequest_Dec");

            entity.HasOne(d => d.InAccount).WithMany(p => p.MatchRequestInAccounts)
                .HasForeignKey(d => d.InAccountId)
                .HasConstraintName("FK_MatchRequest_In");

            entity.HasOne(d => d.Match).WithMany(p => p.MatchRequests)
                .HasForeignKey(d => d.MatchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MatchRequest_Match");

            entity.HasOne(d => d.OutAccount).WithMany(p => p.MatchRequestOutAccounts)
                .HasForeignKey(d => d.OutAccountId)
                .HasConstraintName("FK_MatchRequest_Out");

            entity.HasOne(d => d.RequestedByAccount).WithMany(p => p.MatchRequestRequestedByAccounts)
                .HasForeignKey(d => d.RequestedByAccountId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MatchRequest_By");

            entity.HasOne(d => d.Team).WithMany(p => p.MatchRequests)
                .HasForeignKey(d => d.TeamId)
                .HasConstraintName("FK_MatchRequest_Team");
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.NotificationId).HasName("PRIMARY");

            entity
                .ToTable("Notification")
                .UseCollation("utf8mb4_0900_as_ci");

            entity.HasIndex(e => new { e.AccountId, e.IsRead, e.SentAt }, "IX_Notification_Inbox");

            entity.Property(e => e.NotificationId).HasColumnName("NotificationID");
            entity.Property(e => e.AccountId).HasColumnName("AccountID");
            entity.Property(e => e.Body).HasMaxLength(1000);
            entity.Property(e => e.Channel)
                .HasMaxLength(20)
                .HasDefaultValueSql("'IN_APP'");
            entity.Property(e => e.LinkUrl).HasMaxLength(500);
            entity.Property(e => e.NotificationType).HasMaxLength(30);
            entity.Property(e => e.SentAt)
                .HasDefaultValueSql("utc_timestamp()")
                .HasColumnType("datetime");
            entity.Property(e => e.Title).HasMaxLength(200);

            entity.HasOne(d => d.Account).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.AccountId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Notification_Account");
        });

        modelBuilder.Entity<Organization>(entity =>
        {
            entity.HasKey(e => e.OrganizationId).HasName("PRIMARY");

            entity
                .ToTable("Organization")
                .UseCollation("utf8mb4_0900_as_ci");

            entity.HasIndex(e => e.CreatedByAccountId, "FK_Organization_Creator");

            entity.HasIndex(e => e.ReviewedByAccountId, "FK_Organization_Reviewer");

            entity.HasIndex(e => e.Name, "UQ_Organization_Name").IsUnique();

            entity.Property(e => e.OrganizationId).HasColumnName("OrganizationID");
            entity.Property(e => e.ContactEmail).HasMaxLength(256);
            entity.Property(e => e.ContactPhone).HasMaxLength(20);
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("utc_timestamp()")
                .HasColumnType("datetime");
            entity.Property(e => e.CreatedByAccountId).HasColumnName("CreatedByAccountID");
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.LogoUrl).HasMaxLength(500);
            entity.Property(e => e.Name).HasMaxLength(150);
            entity.Property(e => e.OrganizationType).HasMaxLength(20);
            entity.Property(e => e.ParentOrganization).HasMaxLength(150);
            entity.Property(e => e.ReviewReason).HasMaxLength(1000);
            entity.Property(e => e.ReviewedAt).HasColumnType("datetime");
            entity.Property(e => e.ReviewedByAccountId).HasColumnName("ReviewedByAccountID");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValueSql("'PENDING'");
            entity.Property(e => e.SuspendedAt).HasColumnType("datetime");
            entity.Property(e => e.SuspensionReason).HasMaxLength(1000);
            entity.Property(e => e.WebsiteUrl).HasMaxLength(500);

            entity.HasOne(d => d.CreatedByAccount).WithMany(p => p.OrganizationCreatedByAccounts)
                .HasForeignKey(d => d.CreatedByAccountId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Organization_Creator");

            entity.HasOne(d => d.ReviewedByAccount).WithMany(p => p.OrganizationReviewedByAccounts)
                .HasForeignKey(d => d.ReviewedByAccountId)
                .HasConstraintName("FK_Organization_Reviewer");
        });

        modelBuilder.Entity<OrganizationDocument>(entity =>
        {
            entity.HasKey(e => e.OrganizationDocumentId).HasName("PRIMARY");

            entity
                .ToTable("OrganizationDocument")
                .UseCollation("utf8mb4_0900_as_ci");

            entity.HasIndex(e => e.OrganizationId, "FK_OrganizationDocument_Org");

            entity.Property(e => e.OrganizationDocumentId).HasColumnName("OrganizationDocumentID");
            entity.Property(e => e.DocumentType).HasMaxLength(100);
            entity.Property(e => e.FileUrl).HasMaxLength(500);
            entity.Property(e => e.OrganizationId).HasColumnName("OrganizationID");
            entity.Property(e => e.OriginalFileName).HasMaxLength(255);
            entity.Property(e => e.UploadedAt)
                .HasDefaultValueSql("utc_timestamp()")
                .HasColumnType("datetime");

            entity.HasOne(d => d.Organization).WithMany(p => p.OrganizationDocuments)
                .HasForeignKey(d => d.OrganizationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OrganizationDocument_Org");
        });

        modelBuilder.Entity<OrganizationMember>(entity =>
        {
            entity.HasKey(e => e.OrganizationMemberId).HasName("PRIMARY");

            entity
                .ToTable("OrganizationMember")
                .UseCollation("utf8mb4_0900_as_ci");

            entity.HasIndex(e => e.AccountId, "FK_OrganizationMember_Account");

            entity.HasIndex(e => e.OrganizationId, "FK_OrganizationMember_Org");

            entity.Property(e => e.OrganizationMemberId).HasColumnName("OrganizationMemberID");
            entity.Property(e => e.AccountId).HasColumnName("AccountID");
            entity.Property(e => e.InvitedAt)
                .HasDefaultValueSql("utc_timestamp()")
                .HasColumnType("datetime");
            entity.Property(e => e.JoinedAt).HasColumnType("datetime");
            entity.Property(e => e.OrganizationId).HasColumnName("OrganizationID");
            entity.Property(e => e.Role).HasMaxLength(30);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValueSql("'INVITED'");

            entity.HasOne(d => d.Account).WithMany(p => p.OrganizationMembers)
                .HasForeignKey(d => d.AccountId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OrganizationMember_Account");

            entity.HasOne(d => d.Organization).WithMany(p => p.OrganizationMembers)
                .HasForeignKey(d => d.OrganizationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OrganizationMember_Org");
        });

        modelBuilder.Entity<PaymentRecord>(entity =>
        {
            entity.HasKey(e => e.PaymentRecordId).HasName("PRIMARY");

            entity
                .ToTable("PaymentRecord")
                .UseCollation("utf8mb4_0900_as_ci");

            entity.HasIndex(e => e.RecordedByAccountId, "FK_PaymentRecord_By");

            entity.HasIndex(e => e.RegistrationId, "FK_PaymentRecord_Reg");

            entity.Property(e => e.PaymentRecordId).HasColumnName("PaymentRecordID");
            entity.Property(e => e.DeclaredAmount).HasPrecision(18, 2);
            entity.Property(e => e.FileUrl).HasMaxLength(500);
            entity.Property(e => e.IsCurrent)
                .IsRequired()
                .HasDefaultValueSql("'1'");
            entity.Property(e => e.Note).HasMaxLength(500);
            entity.Property(e => e.RecordKind).HasMaxLength(20);
            entity.Property(e => e.RecordedAt)
                .HasDefaultValueSql("utc_timestamp()")
                .HasColumnType("datetime");
            entity.Property(e => e.RecordedByAccountId).HasColumnName("RecordedByAccountID");
            entity.Property(e => e.RegistrationId).HasColumnName("RegistrationID");

            entity.HasOne(d => d.RecordedByAccount).WithMany(p => p.PaymentRecords)
                .HasForeignKey(d => d.RecordedByAccountId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PaymentRecord_By");

            entity.HasOne(d => d.Registration).WithMany(p => p.PaymentRecords)
                .HasForeignKey(d => d.RegistrationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PaymentRecord_Reg");
        });

        modelBuilder.Entity<Penalty>(entity =>
        {
            entity.HasKey(e => e.PenaltyId).HasName("PRIMARY");

            entity
                .ToTable("Penalty")
                .UseCollation("utf8mb4_0900_as_ci");

            entity.HasIndex(e => e.IssuedByAccountId, "FK_Penalty_By");

            entity.HasIndex(e => e.MatchId, "FK_Penalty_Match");

            entity.HasIndex(e => e.TeamId, "FK_Penalty_Team");

            entity.HasIndex(e => new { e.TournamentId, e.TeamId, e.Status }, "IX_Penalty_TeamTour");

            entity.HasIndex(e => e.CancelsPenaltyId, "UX_Penalty_CancelsOnce").IsUnique();

            entity.Property(e => e.PenaltyId).HasColumnName("PenaltyID");
            entity.Property(e => e.CancelReason).HasMaxLength(500);
            entity.Property(e => e.CancelsPenaltyId).HasColumnName("CancelsPenaltyID");
            entity.Property(e => e.IssuedAt)
                .HasDefaultValueSql("utc_timestamp()")
                .HasColumnType("datetime");
            entity.Property(e => e.IssuedByAccountId).HasColumnName("IssuedByAccountID");
            entity.Property(e => e.MatchId).HasColumnName("MatchID");
            entity.Property(e => e.Points).HasDefaultValueSql("'1'");
            entity.Property(e => e.Reason).HasMaxLength(1000);
            entity.Property(e => e.Severity).HasMaxLength(20);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValueSql("'ACTIVE'");
            entity.Property(e => e.TeamId).HasColumnName("TeamID");
            entity.Property(e => e.TournamentId).HasColumnName("TournamentID");

            entity.HasOne(d => d.CancelsPenalty).WithOne(p => p.InverseCancelsPenalty)
                .HasForeignKey<Penalty>(d => d.CancelsPenaltyId)
                .HasConstraintName("FK_Penalty_Cancels");

            entity.HasOne(d => d.IssuedByAccount).WithMany(p => p.Penalties)
                .HasForeignKey(d => d.IssuedByAccountId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Penalty_By");

            entity.HasOne(d => d.Match).WithMany(p => p.Penalties)
                .HasForeignKey(d => d.MatchId)
                .HasConstraintName("FK_Penalty_Match");

            entity.HasOne(d => d.Team).WithMany(p => p.Penalties)
                .HasForeignKey(d => d.TeamId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Penalty_Team");

            entity.HasOne(d => d.Tournament).WithMany(p => p.Penalties)
                .HasForeignKey(d => d.TournamentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Penalty_Tour");
        });

        modelBuilder.Entity<PlayerCredential>(entity =>
        {
            entity.HasKey(e => e.PlayerCredentialId).HasName("PRIMARY");

            entity
                .ToTable("PlayerCredential")
                .UseCollation("utf8mb4_0900_as_ci");

            entity.HasIndex(e => e.AccountId, "FK_PlayerCredential_Account");

            entity.HasIndex(e => e.GameId, "FK_PlayerCredential_Game");

            entity.Property(e => e.PlayerCredentialId).HasColumnName("PlayerCredentialID");
            entity.Property(e => e.AccountId).HasColumnName("AccountID");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("utc_timestamp()")
                .HasColumnType("datetime");
            entity.Property(e => e.CredentialType).HasMaxLength(20);
            entity.Property(e => e.FileUrl).HasMaxLength(500);
            entity.Property(e => e.GameId).HasColumnName("GameID");
            entity.Property(e => e.Value).HasMaxLength(150);
            entity.Property(e => e.VerifiedAt).HasColumnType("datetime");

            entity.HasOne(d => d.Account).WithMany(p => p.PlayerCredentials)
                .HasForeignKey(d => d.AccountId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PlayerCredential_Account");

            entity.HasOne(d => d.Game).WithMany(p => p.PlayerCredentials)
                .HasForeignKey(d => d.GameId)
                .HasConstraintName("FK_PlayerCredential_Game");
        });

        modelBuilder.Entity<PromotionLink>(entity =>
        {
            entity.HasKey(e => e.PromotionLinkId).HasName("PRIMARY");

            entity
                .ToTable("PromotionLink")
                .UseCollation("utf8mb4_0900_as_ci");

            entity.HasIndex(e => e.CreatedByAccountId, "FK_PromotionLink_By");

            entity.HasIndex(e => e.TournamentId, "FK_PromotionLink_Tour");

            entity.HasIndex(e => e.Code, "UQ_PromotionLink_Code").IsUnique();

            entity.Property(e => e.PromotionLinkId).HasColumnName("PromotionLinkID");
            entity.Property(e => e.Code).HasMaxLength(20);
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("utc_timestamp()")
                .HasColumnType("datetime");
            entity.Property(e => e.CreatedByAccountId).HasColumnName("CreatedByAccountID");
            entity.Property(e => e.Label).HasMaxLength(150);
            entity.Property(e => e.TournamentId).HasColumnName("TournamentID");

            entity.HasOne(d => d.CreatedByAccount).WithMany(p => p.PromotionLinks)
                .HasForeignKey(d => d.CreatedByAccountId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PromotionLink_By");

            entity.HasOne(d => d.Tournament).WithMany(p => p.PromotionLinks)
                .HasForeignKey(d => d.TournamentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PromotionLink_Tour");
        });

        modelBuilder.Entity<RecruitmentPost>(entity =>
        {
            entity.HasKey(e => e.RecruitmentPostId).HasName("PRIMARY");

            entity
                .ToTable("RecruitmentPost")
                .UseCollation("utf8mb4_0900_as_ci");

            entity.HasIndex(e => e.AuthorAccountId, "FK_RecruitmentPost_Author");

            entity.HasIndex(e => e.GameId, "FK_RecruitmentPost_Game");

            entity.HasIndex(e => e.TeamId, "FK_RecruitmentPost_Team");

            entity.Property(e => e.RecruitmentPostId).HasColumnName("RecruitmentPostID");
            entity.Property(e => e.AuthorAccountId).HasColumnName("AuthorAccountID");
            entity.Property(e => e.Content).HasColumnType("mediumtext");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("utc_timestamp()")
                .HasColumnType("datetime");
            entity.Property(e => e.GameId).HasColumnName("GameID");
            entity.Property(e => e.HighlightUrl).HasMaxLength(500);
            entity.Property(e => e.PostType).HasMaxLength(20);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValueSql("'OPEN'");
            entity.Property(e => e.TeamId).HasColumnName("TeamID");
            entity.Property(e => e.Title).HasMaxLength(200);

            entity.HasOne(d => d.AuthorAccount).WithMany(p => p.RecruitmentPosts)
                .HasForeignKey(d => d.AuthorAccountId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_RecruitmentPost_Author");

            entity.HasOne(d => d.Game).WithMany(p => p.RecruitmentPosts)
                .HasForeignKey(d => d.GameId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_RecruitmentPost_Game");

            entity.HasOne(d => d.Team).WithMany(p => p.RecruitmentPosts)
                .HasForeignKey(d => d.TeamId)
                .HasConstraintName("FK_RecruitmentPost_Team");
        });

        modelBuilder.Entity<Registration>(entity =>
        {
            entity.HasKey(e => e.RegistrationId).HasName("PRIMARY");

            entity
                .ToTable("Registration")
                .UseCollation("utf8mb4_0900_as_ci");

            entity.HasIndex(e => e.SubmittedByAccountId, "FK_Registration_By");

            entity.HasIndex(e => e.TeamId, "FK_Registration_Team");

            entity.HasIndex(e => e.TournamentId, "FK_Registration_Tour");

            entity.Property(e => e.RegistrationId).HasColumnName("RegistrationID");
            entity.Property(e => e.ConsentAt).HasColumnType("datetime");
            entity.Property(e => e.FinalPlacement)
                .HasMaxLength(20)
                .HasDefaultValueSql("'NONE'");
            entity.Property(e => e.PaymentStatus)
                .HasMaxLength(20)
                .HasDefaultValueSql("'UNPAID'");
            entity.Property(e => e.ReviewNote).HasMaxLength(500);
            entity.Property(e => e.ReviewedAt).HasColumnType("datetime");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValueSql("'DRAFT'");
            entity.Property(e => e.SubmittedAt).HasColumnType("datetime");
            entity.Property(e => e.SubmittedByAccountId).HasColumnName("SubmittedByAccountID");
            entity.Property(e => e.TeamId).HasColumnName("TeamID");
            entity.Property(e => e.TournamentId).HasColumnName("TournamentID");

            entity.HasOne(d => d.SubmittedByAccount).WithMany(p => p.Registrations)
                .HasForeignKey(d => d.SubmittedByAccountId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Registration_By");

            entity.HasOne(d => d.Team).WithMany(p => p.Registrations)
                .HasForeignKey(d => d.TeamId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Registration_Team");

            entity.HasOne(d => d.Tournament).WithMany(p => p.Registrations)
                .HasForeignKey(d => d.TournamentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Registration_Tour");
        });

        modelBuilder.Entity<RegistrationLineup>(entity =>
        {
            entity.HasKey(e => e.RegistrationLineupId).HasName("PRIMARY");

            entity
                .ToTable("RegistrationLineup")
                .UseCollation("utf8mb4_0900_as_ci");

            entity.HasIndex(e => e.AccountId, "FK_RegistrationLineup_Acc");

            entity.HasIndex(e => e.PlayerCredentialId, "FK_RegistrationLineup_Cred");

            entity.HasIndex(e => new { e.RegistrationId, e.AccountId }, "UQ_RegistrationLineup_User").IsUnique();

            entity.Property(e => e.RegistrationLineupId).HasColumnName("RegistrationLineupID");
            entity.Property(e => e.AccountId).HasColumnName("AccountID");
            entity.Property(e => e.InGameNameSnapshot).HasMaxLength(150);
            entity.Property(e => e.PlayerCredentialId).HasColumnName("PlayerCredentialID");
            entity.Property(e => e.RegistrationId).HasColumnName("RegistrationID");
            entity.Property(e => e.Slot).HasMaxLength(20);

            entity.HasOne(d => d.Account).WithMany(p => p.RegistrationLineups)
                .HasForeignKey(d => d.AccountId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_RegistrationLineup_Acc");

            entity.HasOne(d => d.PlayerCredential).WithMany(p => p.RegistrationLineups)
                .HasForeignKey(d => d.PlayerCredentialId)
                .HasConstraintName("FK_RegistrationLineup_Cred");

            entity.HasOne(d => d.Registration).WithMany(p => p.RegistrationLineups)
                .HasForeignKey(d => d.RegistrationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_RegistrationLineup_Reg");
        });

        modelBuilder.Entity<Team>(entity =>
        {
            entity.HasKey(e => e.TeamId).HasName("PRIMARY");

            entity
                .ToTable("Team")
                .UseCollation("utf8mb4_0900_as_ci");

            entity.HasIndex(e => e.CaptainAccountId, "FK_Team_Captain");

            entity.HasIndex(e => new { e.GameId, e.Name }, "UQ_Team_Name").IsUnique();

            entity.HasIndex(e => new { e.GameId, e.Tag }, "UQ_Team_Tag").IsUnique();

            entity.HasIndex(e => e.InviteCode, "UX_Team_InviteCode").IsUnique();

            entity.Property(e => e.TeamId).HasColumnName("TeamID");
            entity.Property(e => e.CaptainAccountId).HasColumnName("CaptainAccountID");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("utc_timestamp()")
                .HasColumnType("datetime");
            entity.Property(e => e.GameId).HasColumnName("GameID");
            entity.Property(e => e.InviteCode).HasMaxLength(20);
            entity.Property(e => e.InviteExpiresAt).HasColumnType("datetime");
            entity.Property(e => e.LogoUrl).HasMaxLength(500);
            entity.Property(e => e.Name).HasMaxLength(150);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValueSql("'ACTIVE'");
            entity.Property(e => e.Tag).HasMaxLength(10);

            entity.HasOne(d => d.CaptainAccount).WithMany(p => p.Teams)
                .HasForeignKey(d => d.CaptainAccountId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Team_Captain");

            entity.HasOne(d => d.Game).WithMany(p => p.Teams)
                .HasForeignKey(d => d.GameId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Team_Game");
        });

        modelBuilder.Entity<TeamMember>(entity =>
        {
            entity.HasKey(e => e.TeamMemberId).HasName("PRIMARY");

            entity
                .ToTable("TeamMember")
                .UseCollation("utf8mb4_0900_as_ci");

            entity.HasIndex(e => e.AccountId, "FK_TeamMember_Account");

            entity.HasIndex(e => e.GameId, "FK_TeamMember_Game");

            entity.HasIndex(e => e.RecruitmentPostId, "FK_TeamMember_Post");

            entity.HasIndex(e => e.TeamId, "FK_TeamMember_Team");

            entity.Property(e => e.TeamMemberId).HasColumnName("TeamMemberID");
            entity.Property(e => e.AccountId).HasColumnName("AccountID");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("utc_timestamp()")
                .HasColumnType("datetime");
            entity.Property(e => e.Direction).HasMaxLength(20);
            entity.Property(e => e.GameId).HasColumnName("GameID");
            entity.Property(e => e.JoinedAt).HasColumnType("datetime");
            entity.Property(e => e.LeftAt).HasColumnType("datetime");
            entity.Property(e => e.Message).HasMaxLength(500);
            entity.Property(e => e.RecruitmentPostId).HasColumnName("RecruitmentPostID");
            entity.Property(e => e.Slot)
                .HasMaxLength(20)
                .HasDefaultValueSql("'STARTER'");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValueSql("'PENDING'");
            entity.Property(e => e.TeamId).HasColumnName("TeamID");

            entity.HasOne(d => d.Account).WithMany(p => p.TeamMembers)
                .HasForeignKey(d => d.AccountId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TeamMember_Account");

            entity.HasOne(d => d.Game).WithMany(p => p.TeamMembers)
                .HasForeignKey(d => d.GameId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TeamMember_Game");

            entity.HasOne(d => d.RecruitmentPost).WithMany(p => p.TeamMembers)
                .HasForeignKey(d => d.RecruitmentPostId)
                .HasConstraintName("FK_TeamMember_Post");

            entity.HasOne(d => d.Team).WithMany(p => p.TeamMembers)
                .HasForeignKey(d => d.TeamId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TeamMember_Team");
        });

        modelBuilder.Entity<Tournament>(entity =>
        {
            entity.HasKey(e => e.TournamentId).HasName("PRIMARY");

            entity
                .ToTable("Tournament")
                .UseCollation("utf8mb4_0900_as_ci");

            entity.HasIndex(e => e.GameId, "FK_Tournament_Game");

            entity.HasIndex(e => e.BracketLockedByAccountId, "FK_Tournament_LockedBy");

            entity.HasIndex(e => e.OrganizerAccountId, "FK_Tournament_Organizer");

            entity.HasIndex(e => new { e.OrganizationId, e.Status }, "IX_Tournament_Organization");

            entity.Property(e => e.TournamentId).HasColumnName("TournamentID");
            entity.Property(e => e.BankAccountInfo).HasMaxLength(300);
            entity.Property(e => e.BannerUrl).HasMaxLength(500);
            entity.Property(e => e.BestOfPerRound).HasMaxLength(200);
            entity.Property(e => e.BracketLockedAt).HasColumnType("datetime");
            entity.Property(e => e.BracketLockedByAccountId).HasColumnName("BracketLockedByAccountID");
            entity.Property(e => e.CancelReason).HasMaxLength(500);
            entity.Property(e => e.ContactInfo).HasMaxLength(300);
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("utc_timestamp()")
                .HasColumnType("datetime");
            entity.Property(e => e.Description).HasColumnType("mediumtext");
            entity.Property(e => e.EndAt).HasColumnType("datetime");
            entity.Property(e => e.EntryFee).HasPrecision(18, 2);
            entity.Property(e => e.Format).HasMaxLength(30);
            entity.Property(e => e.GameId).HasColumnName("GameID");
            entity.Property(e => e.IsOnline)
                .IsRequired()
                .HasDefaultValueSql("'1'");
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.OrganizationId).HasColumnName("OrganizationID");
            entity.Property(e => e.OrganizerAccountId).HasColumnName("OrganizerAccountID");
            entity.Property(e => e.OrganizingUnit).HasMaxLength(200);
            entity.Property(e => e.PrizeStructure).HasColumnType("mediumtext");
            entity.Property(e => e.RegCloseAt).HasColumnType("datetime");
            entity.Property(e => e.RegOpenAt).HasColumnType("datetime");
            entity.Property(e => e.Region).HasMaxLength(100);
            entity.Property(e => e.RequireIdentityDocument)
                .IsRequired()
                .HasDefaultValueSql("'1'");
            entity.Property(e => e.RequireInGameName)
                .IsRequired()
                .HasDefaultValueSql("'1'");
            entity.Property(e => e.RequirePaymentProof)
                .IsRequired()
                .HasDefaultValueSql("'1'");
            entity.Property(e => e.RulebookContent).HasColumnType("mediumtext");
            entity.Property(e => e.RulebookVersion).HasDefaultValueSql("'1'");
            entity.Property(e => e.StartAt).HasColumnType("datetime");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValueSql("'DRAFT'");
            entity.Property(e => e.StreamUrl).HasMaxLength(500);
            entity.Property(e => e.TieBreakerOrder).HasMaxLength(200);
            entity.Property(e => e.Venue).HasMaxLength(300);

            entity.HasOne(d => d.BracketLockedByAccount).WithMany(p => p.TournamentBracketLockedByAccounts)
                .HasForeignKey(d => d.BracketLockedByAccountId)
                .HasConstraintName("FK_Tournament_LockedBy");

            entity.HasOne(d => d.Game).WithMany(p => p.Tournaments)
                .HasForeignKey(d => d.GameId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Tournament_Game");

            entity.HasOne(d => d.Organization).WithMany(p => p.Tournaments)
                .HasForeignKey(d => d.OrganizationId)
                .HasConstraintName("FK_Tournament_Organization");

            entity.HasOne(d => d.OrganizerAccount).WithMany(p => p.TournamentOrganizerAccounts)
                .HasForeignKey(d => d.OrganizerAccountId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Tournament_Organizer");
        });

        modelBuilder.Entity<TournamentFollow>(entity =>
        {
            entity.HasKey(e => e.TournamentFollowId).HasName("PRIMARY");

            entity
                .ToTable("TournamentFollow")
                .UseCollation("utf8mb4_0900_as_ci");

            entity.HasIndex(e => e.TournamentId, "FK_TournamentFollow_Tour");

            entity.HasIndex(e => new { e.AccountId, e.TournamentId }, "UQ_TournamentFollow").IsUnique();

            entity.Property(e => e.TournamentFollowId).HasColumnName("TournamentFollowID");
            entity.Property(e => e.AccountId).HasColumnName("AccountID");
            entity.Property(e => e.FollowedAt)
                .HasDefaultValueSql("utc_timestamp()")
                .HasColumnType("datetime");
            entity.Property(e => e.TournamentId).HasColumnName("TournamentID");

            entity.HasOne(d => d.Account).WithMany(p => p.TournamentFollows)
                .HasForeignKey(d => d.AccountId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TournamentFollow_Acc");

            entity.HasOne(d => d.Tournament).WithMany(p => p.TournamentFollows)
                .HasForeignKey(d => d.TournamentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TournamentFollow_Tour");
        });

        modelBuilder.Entity<TournamentSponsor>(entity =>
        {
            entity.HasKey(e => e.TournamentSponsorId).HasName("PRIMARY");

            entity
                .ToTable("TournamentSponsor")
                .UseCollation("utf8mb4_0900_as_ci");

            entity.HasIndex(e => e.CreatedByAccountId, "FK_TournamentSponsor_By");

            entity.HasIndex(e => new { e.TournamentId, e.Name }, "UQ_TournamentSponsor_Name").IsUnique();

            entity.Property(e => e.TournamentSponsorId).HasColumnName("TournamentSponsorID");
            entity.Property(e => e.ContactInfo).HasMaxLength(300);
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("utc_timestamp()")
                .HasColumnType("datetime");
            entity.Property(e => e.CreatedByAccountId).HasColumnName("CreatedByAccountID");
            entity.Property(e => e.DisplayOrder).HasDefaultValueSql("'1'");
            entity.Property(e => e.LogoUrl).HasMaxLength(500);
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.ShowWhenCancelled)
                .IsRequired()
                .HasDefaultValueSql("'1'");
            entity.Property(e => e.Tier).HasMaxLength(20);
            entity.Property(e => e.TournamentId).HasColumnName("TournamentID");
            entity.Property(e => e.WebsiteUrl).HasMaxLength(500);

            entity.HasOne(d => d.CreatedByAccount).WithMany(p => p.TournamentSponsors)
                .HasForeignKey(d => d.CreatedByAccountId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TournamentSponsor_By");

            entity.HasOne(d => d.Tournament).WithMany(p => p.TournamentSponsors)
                .HasForeignKey(d => d.TournamentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TournamentSponsor_Tour");
        });

        modelBuilder.Entity<VwGroupStanding>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_GroupStanding");

            entity.Property(e => e.GroupLabel)
                .HasMaxLength(10)
                .UseCollation("utf8mb4_0900_as_ci");
            entity.Property(e => e.Lost).HasPrecision(23);
            entity.Property(e => e.Point).HasPrecision(23);
            entity.Property(e => e.ScoreDiff).HasPrecision(33);
            entity.Property(e => e.TeamId).HasColumnName("TeamID");
            entity.Property(e => e.TournamentId).HasColumnName("TournamentID");
            entity.Property(e => e.Won).HasPrecision(23);
        });

        modelBuilder.Entity<VwLeaderboard>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_Leaderboard");

            entity.Property(e => e.AchievementPoint).HasPrecision(26);
            entity.Property(e => e.GameId).HasColumnName("GameID");
            entity.Property(e => e.Season).HasColumnType("year");
            entity.Property(e => e.TeamId).HasColumnName("TeamID");
            entity.Property(e => e.TitleCount).HasPrecision(23);
        });

        modelBuilder.Entity<VwOrganizationReputation>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_OrganizationReputation");

            entity.Property(e => e.AvgResolveHours).HasPrecision(9, 1);
            entity.Property(e => e.CompletedCount).HasPrecision(23);
            entity.Property(e => e.CompletedPercent).HasPrecision(5, 1);
            entity.Property(e => e.DisputedPercent).HasPrecision(5, 1);
            entity.Property(e => e.Name)
                .HasMaxLength(150)
                .UseCollation("utf8mb4_0900_as_ci");
            entity.Property(e => e.OrganizationId).HasColumnName("OrganizationID");
        });

        modelBuilder.Entity<VwTeamPenaltyPoint>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_TeamPenaltyPoint");

            entity.Property(e => e.ActivePoints).HasPrecision(32);
            entity.Property(e => e.TeamId).HasColumnName("TeamID");
            entity.Property(e => e.TournamentId).HasColumnName("TournamentID");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
