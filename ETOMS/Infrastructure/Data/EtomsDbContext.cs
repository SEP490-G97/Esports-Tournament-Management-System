using System;
using System.Collections.Generic;
using ETOMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ETOMS.Infrastructure.Data;

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

    public virtual DbSet<Dispute> Disputes { get; set; }

    public virtual DbSet<Game> Games { get; set; }

    public virtual DbSet<Livestream> Livestreams { get; set; }

    public virtual DbSet<Match> Matches { get; set; }

    public virtual DbSet<MatchChatMessage> MatchChatMessages { get; set; }

    public virtual DbSet<MatchGame> MatchGames { get; set; }

    public virtual DbSet<MatchReport> MatchReports { get; set; }

    public virtual DbSet<MatchRequest> MatchRequests { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<PaymentRecord> PaymentRecords { get; set; }

    public virtual DbSet<Penalty> Penalties { get; set; }

    public virtual DbSet<PlayerCredential> PlayerCredentials { get; set; }

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

    public virtual DbSet<VwTeamPenaltyPoint> VwTeamPenaltyPoints { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.UseCollation("Vietnamese_CI_AS");

        modelBuilder.Entity<Account>(entity =>
        {
            entity.ToTable("Account");

            entity.HasIndex(e => e.Email, "UQ_Account_Email").IsUnique();

            entity.HasIndex(e => e.GoogleSubjectId, "UX_Account_Google")
                .IsUnique()
                .HasFilter("([GoogleSubjectId] IS NOT NULL)");

            entity.Property(e => e.AccountId).HasColumnName("AccountID");
            entity.Property(e => e.AvatarUrl).HasMaxLength(500);
            entity.Property(e => e.BanReason).HasMaxLength(500);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_Account_At");
            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property(e => e.EmailVerifiedAt).HasPrecision(0);
            entity.Property(e => e.FullName).HasMaxLength(150);
            entity.Property(e => e.GoogleSubjectId)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.NotifyEmail).HasDefaultValue(true, "DF_Account_NotiMail");
            entity.Property(e => e.NotifyInApp).HasDefaultValue(true, "DF_Account_NotiApp");
            entity.Property(e => e.PasswordHash)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Phone)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Role)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("PLAYER", "DF_Account_Role");
            entity.Property(e => e.School).HasMaxLength(200);
            entity.Property(e => e.SocialLinks).HasMaxLength(500);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("PENDING_VERIFY", "DF_Account_Status");
        });

        modelBuilder.Entity<Attachment>(entity =>
        {
            entity.ToTable("Attachment");

            entity.HasIndex(e => new { e.OwnerType, e.OwnerId }, "IX_Attachment_Owner");

            entity.Property(e => e.AttachmentId).HasColumnName("AttachmentID");
            entity.Property(e => e.FileType)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.FileUrl).HasMaxLength(500);
            entity.Property(e => e.OwnerId).HasColumnName("OwnerID");
            entity.Property(e => e.OwnerType)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.UploadedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_Attachment_At");
            entity.Property(e => e.UploadedByAccountId).HasColumnName("UploadedByAccountID");

            entity.HasOne(d => d.UploadedByAccount).WithMany(p => p.Attachments)
                .HasForeignKey(d => d.UploadedByAccountId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Attachment_By");
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("AuditLog");

            entity.HasIndex(e => new { e.TournamentId, e.CreatedAt }, "IX_AuditLog_Timeline");

            entity.Property(e => e.AuditLogId).HasColumnName("AuditLogID");
            entity.Property(e => e.ActionType)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.ActorAccountId).HasColumnName("ActorAccountID");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_AuditLog_At");
            entity.Property(e => e.ObjectId).HasColumnName("ObjectID");
            entity.Property(e => e.ObjectType)
                .HasMaxLength(30)
                .IsUnicode(false);
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
            entity.ToTable("AuthToken");

            entity.HasIndex(e => e.TokenHash, "UQ_AuthToken_Hash").IsUnique();

            entity.Property(e => e.AuthTokenId).HasColumnName("AuthTokenID");
            entity.Property(e => e.AccountId).HasColumnName("AccountID");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_AuthToken_At");
            entity.Property(e => e.ExpiresAt).HasPrecision(0);
            entity.Property(e => e.TokenHash)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.TokenType)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.UsedAt).HasPrecision(0);

            entity.HasOne(d => d.Account).WithMany(p => p.AuthTokens)
                .HasForeignKey(d => d.AccountId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AuthToken_Account");
        });

        modelBuilder.Entity<BracketNode>(entity =>
        {
            entity.ToTable("BracketNode");

            entity.HasIndex(e => e.ParentNodeId, "IX_BracketNode_Parent");

            entity.HasIndex(e => new { e.TournamentId, e.StageNo, e.RoundNo }, "IX_BracketNode_Tour");

            entity.Property(e => e.BracketNodeId).HasColumnName("BracketNodeID");
            entity.Property(e => e.GeneratedAt).HasPrecision(0);
            entity.Property(e => e.GroupLabel).HasMaxLength(10);
            entity.Property(e => e.NodeType)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.ParentNodeId).HasColumnName("ParentNodeID");
            entity.Property(e => e.SeedTeamId).HasColumnName("SeedTeamID");
            entity.Property(e => e.SeedingMethod)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("MANUAL", "DF_BrNode_Seeding");
            entity.Property(e => e.StageNo).HasDefaultValue(1, "DF_BrNode_Stage");
            entity.Property(e => e.StageType)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("DRAFT", "DF_BrNode_Status");
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

        modelBuilder.Entity<Dispute>(entity =>
        {
            entity.ToTable("Dispute");

            entity.HasIndex(e => e.MatchId, "UX_Dispute_LivePerMatch")
                .IsUnique()
                .HasFilter("([Status]<>'CLOSED' AND [Status]<>'RESOLVED')");

            entity.Property(e => e.DisputeId).HasColumnName("DisputeID");
            entity.Property(e => e.AppealReason).HasMaxLength(1000);
            entity.Property(e => e.AppealStatus)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("NONE", "DF_Dispute_AppealS");
            entity.Property(e => e.FlagCategory)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.FlagNote).HasMaxLength(1000);
            entity.Property(e => e.FlaggedByAccountId).HasColumnName("FlaggedByAccountID");
            entity.Property(e => e.MatchId).HasColumnName("MatchID");
            entity.Property(e => e.OpenReason)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.OpenedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_Dispute_At");
            entity.Property(e => e.ResolvedAt).HasPrecision(0);
            entity.Property(e => e.ResolvedByAccountId).HasColumnName("ResolvedByAccountID");
            entity.Property(e => e.RulingReason).HasMaxLength(1000);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("OPEN", "DF_Dispute_Status");

            entity.HasOne(d => d.FlaggedByAccount).WithMany(p => p.DisputeFlaggedByAccounts)
                .HasForeignKey(d => d.FlaggedByAccountId)
                .HasConstraintName("FK_Dispute_Flagger");

            entity.HasOne(d => d.Match).WithOne(p => p.Dispute)
                .HasForeignKey<Dispute>(d => d.MatchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Dispute_Match");

            entity.HasOne(d => d.ResolvedByAccount).WithMany(p => p.DisputeResolvedByAccounts)
                .HasForeignKey(d => d.ResolvedByAccountId)
                .HasConstraintName("FK_Dispute_Resolver");
        });

        modelBuilder.Entity<Game>(entity =>
        {
            entity.ToTable("Game");

            entity.HasIndex(e => e.ShortCode, "UQ_Game_Code").IsUnique();

            entity.HasIndex(e => e.Title, "UQ_Game_Title").IsUnique();

            entity.Property(e => e.GameId).HasColumnName("GameID");
            entity.Property(e => e.AllowedEvidenceTypes).HasMaxLength(200);
            entity.Property(e => e.BannerUrl).HasMaxLength(500);
            entity.Property(e => e.DefaultFormat)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.LogoUrl).HasMaxLength(500);
            entity.Property(e => e.MaxEvidenceFileSizeMb)
                .HasDefaultValue(10, "DF_Game_MaxSize")
                .HasColumnName("MaxEvidenceFileSizeMB");
            entity.Property(e => e.ShortCode)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("ACTIVE", "DF_Game_Status");
            entity.Property(e => e.Title).HasMaxLength(150);
        });

        modelBuilder.Entity<Livestream>(entity =>
        {
            entity.ToTable("Livestream");

            entity.HasIndex(e => new { e.TournamentId, e.Status }, "IX_Livestream_Tour");

            entity.Property(e => e.LivestreamId).HasColumnName("LivestreamID");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_Livestream_At");
            entity.Property(e => e.CreatedByAccountId).HasColumnName("CreatedByAccountID");
            entity.Property(e => e.MatchId).HasColumnName("MatchID");
            entity.Property(e => e.Platform)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("SCHEDULED", "DF_Livestream_Status");
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
            entity.ToTable("Match");

            entity.HasIndex(e => e.ScheduledAt, "IX_Match_Schedule");

            entity.HasIndex(e => new { e.TournamentId, e.Status }, "IX_Match_Tour");

            entity.Property(e => e.MatchId).HasColumnName("MatchID");
            entity.Property(e => e.BestOf).HasDefaultValue(1, "DF_Match_BestOf");
            entity.Property(e => e.BracketNodeId).HasColumnName("BracketNodeID");
            entity.Property(e => e.CheckInMode)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("SELF", "DF_Match_CiMode");
            entity.Property(e => e.CheckInOpenAt).HasPrecision(0);
            entity.Property(e => e.OverrideReason).HasMaxLength(500);
            entity.Property(e => e.RefereeAccountId).HasColumnName("RefereeAccountID");
            entity.Property(e => e.ReportDeadlineAt).HasPrecision(0);
            entity.Property(e => e.ResultSource)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.RoomCode).HasMaxLength(100);
            entity.Property(e => e.RoomPassword).HasMaxLength(100);
            entity.Property(e => e.ScheduledAt).HasPrecision(0);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("PENDING", "DF_Match_Status");
            entity.Property(e => e.TeamAcheckedInAt)
                .HasPrecision(0)
                .HasColumnName("TeamACheckedInAt");
            entity.Property(e => e.TeamAid).HasColumnName("TeamAID");
            entity.Property(e => e.TeamBcheckedInAt)
                .HasPrecision(0)
                .HasColumnName("TeamBCheckedInAt");
            entity.Property(e => e.TeamBid).HasColumnName("TeamBID");
            entity.Property(e => e.TournamentId).HasColumnName("TournamentID");
            entity.Property(e => e.VerifiedAt).HasPrecision(0);
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
            entity.ToTable("MatchChatMessage");

            entity.HasIndex(e => new { e.MatchId, e.SentAt }, "IX_MatchChatMessage_Match");

            entity.Property(e => e.MatchChatMessageId).HasColumnName("MatchChatMessageID");
            entity.Property(e => e.Content).HasMaxLength(2000);
            entity.Property(e => e.MatchId).HasColumnName("MatchID");
            entity.Property(e => e.ReportNote).HasMaxLength(500);
            entity.Property(e => e.ReportStatus)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("NONE", "DF_MatchChat_Rep");
            entity.Property(e => e.ReportedByAccountId).HasColumnName("ReportedByAccountID");
            entity.Property(e => e.SenderAccountId).HasColumnName("SenderAccountID");
            entity.Property(e => e.SentAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_MatchChat_At");

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
            entity.ToTable("MatchGame");

            entity.HasIndex(e => new { e.MatchId, e.GameNo }, "UQ_MatchGame_Number").IsUnique();

            entity.Property(e => e.MatchGameId).HasColumnName("MatchGameID");
            entity.Property(e => e.MatchId).HasColumnName("MatchID");
            entity.Property(e => e.UpdatedAt).HasPrecision(0);
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
            entity.ToTable("MatchReport");

            entity.HasIndex(e => new { e.MatchId, e.TeamId }, "UQ_MatchReport_TeamMatch").IsUnique();

            entity.Property(e => e.MatchReportId).HasColumnName("MatchReportID");
            entity.Property(e => e.MatchId).HasColumnName("MatchID");
            entity.Property(e => e.PerGameScores).HasMaxLength(200);
            entity.Property(e => e.ReportedByAccountId).HasColumnName("ReportedByAccountID");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("SUBMITTED", "DF_MatchReport_Status");
            entity.Property(e => e.SubmittedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_MatchReport_At");
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
            entity.ToTable("MatchRequest");

            entity.Property(e => e.MatchRequestId).HasColumnName("MatchRequestID");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_MatchReq_At");
            entity.Property(e => e.DecidedAt).HasPrecision(0);
            entity.Property(e => e.DecidedByAccountId).HasColumnName("DecidedByAccountID");
            entity.Property(e => e.InAccountId).HasColumnName("InAccountID");
            entity.Property(e => e.MatchId).HasColumnName("MatchID");
            entity.Property(e => e.OutAccountId).HasColumnName("OutAccountID");
            entity.Property(e => e.Reason).HasMaxLength(500);
            entity.Property(e => e.RequestKind)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.RequestedByAccountId).HasColumnName("RequestedByAccountID");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("PENDING", "DF_MatchReq_Status");
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
            entity.ToTable("Notification");

            entity.HasIndex(e => new { e.AccountId, e.IsRead, e.SentAt }, "IX_Notification_Inbox");

            entity.Property(e => e.NotificationId).HasColumnName("NotificationID");
            entity.Property(e => e.AccountId).HasColumnName("AccountID");
            entity.Property(e => e.Body).HasMaxLength(1000);
            entity.Property(e => e.Channel)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("IN_APP", "DF_Notification_Ch");
            entity.Property(e => e.LinkUrl).HasMaxLength(500);
            entity.Property(e => e.NotificationType)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.SentAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_Notification_At");
            entity.Property(e => e.Title).HasMaxLength(200);

            entity.HasOne(d => d.Account).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.AccountId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Notification_Account");
        });

        modelBuilder.Entity<PaymentRecord>(entity =>
        {
            entity.ToTable("PaymentRecord");

            entity.HasIndex(e => e.RegistrationId, "UX_PaymentRecord_CurrentProof")
                .IsUnique()
                .HasFilter("([RecordKind]='PROOF' AND [IsCurrent]=(1))");

            entity.Property(e => e.PaymentRecordId).HasColumnName("PaymentRecordID");
            entity.Property(e => e.DeclaredAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.FileUrl).HasMaxLength(500);
            entity.Property(e => e.IsCurrent).HasDefaultValue(true, "DF_PayRec_Current");
            entity.Property(e => e.Note).HasMaxLength(500);
            entity.Property(e => e.RecordKind)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.RecordedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_PayRec_At");
            entity.Property(e => e.RecordedByAccountId).HasColumnName("RecordedByAccountID");
            entity.Property(e => e.RegistrationId).HasColumnName("RegistrationID");

            entity.HasOne(d => d.RecordedByAccount).WithMany(p => p.PaymentRecords)
                .HasForeignKey(d => d.RecordedByAccountId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PaymentRecord_By");

            entity.HasOne(d => d.Registration).WithOne(p => p.PaymentRecord)
                .HasForeignKey<PaymentRecord>(d => d.RegistrationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PaymentRecord_Reg");
        });

        modelBuilder.Entity<Penalty>(entity =>
        {
            entity.ToTable("Penalty");

            entity.HasIndex(e => new { e.TournamentId, e.TeamId, e.Status }, "IX_Penalty_TeamTour");

            entity.HasIndex(e => e.CancelsPenaltyId, "UX_Penalty_CancelsOnce")
                .IsUnique()
                .HasFilter("([CancelsPenaltyID] IS NOT NULL)");

            entity.Property(e => e.PenaltyId).HasColumnName("PenaltyID");
            entity.Property(e => e.CancelReason).HasMaxLength(500);
            entity.Property(e => e.CancelsPenaltyId).HasColumnName("CancelsPenaltyID");
            entity.Property(e => e.IssuedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_Penalty_At");
            entity.Property(e => e.IssuedByAccountId).HasColumnName("IssuedByAccountID");
            entity.Property(e => e.MatchId).HasColumnName("MatchID");
            entity.Property(e => e.Points).HasDefaultValue(1, "DF_Penalty_Points");
            entity.Property(e => e.Reason).HasMaxLength(1000);
            entity.Property(e => e.Severity)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("ACTIVE", "DF_Penalty_Status");
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
            entity.ToTable("PlayerCredential");

            entity.HasIndex(e => new { e.AccountId, e.GameId }, "UX_PlayerCredential_Ign")
                .IsUnique()
                .HasFilter("([CredentialType]='IN_GAME_NAME')");

            entity.Property(e => e.PlayerCredentialId).HasColumnName("PlayerCredentialID");
            entity.Property(e => e.AccountId).HasColumnName("AccountID");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_PlayerCred_At");
            entity.Property(e => e.CredentialType)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.FileUrl).HasMaxLength(500);
            entity.Property(e => e.GameId).HasColumnName("GameID");
            entity.Property(e => e.Value).HasMaxLength(150);
            entity.Property(e => e.VerifiedAt).HasPrecision(0);

            entity.HasOne(d => d.Account).WithMany(p => p.PlayerCredentials)
                .HasForeignKey(d => d.AccountId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PlayerCredential_Account");

            entity.HasOne(d => d.Game).WithMany(p => p.PlayerCredentials)
                .HasForeignKey(d => d.GameId)
                .HasConstraintName("FK_PlayerCredential_Game");
        });

        modelBuilder.Entity<RecruitmentPost>(entity =>
        {
            entity.ToTable("RecruitmentPost");

            entity.Property(e => e.RecruitmentPostId).HasColumnName("RecruitmentPostID");
            entity.Property(e => e.AuthorAccountId).HasColumnName("AuthorAccountID");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_RecPost_At");
            entity.Property(e => e.GameId).HasColumnName("GameID");
            entity.Property(e => e.HighlightUrl).HasMaxLength(500);
            entity.Property(e => e.PostType)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("OPEN", "DF_RecPost_Status");
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
            entity.ToTable("Registration");

            entity.HasIndex(e => new { e.TournamentId, e.TeamId }, "UX_Registration_OneLivePerTeam")
                .IsUnique()
                .HasFilter("([Status]<>'WITHDRAWN' AND [Status]<>'REJECTED')");

            entity.HasIndex(e => new { e.TournamentId, e.WaitlistPosition }, "UX_Registration_WaitPosition")
                .IsUnique()
                .HasFilter("([Status]='WAITLISTED')");

            entity.Property(e => e.RegistrationId).HasColumnName("RegistrationID");
            entity.Property(e => e.ConsentAt).HasPrecision(0);
            entity.Property(e => e.FinalPlacement)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("NONE", "DF_Reg_Place");
            entity.Property(e => e.PaymentStatus)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("UNPAID", "DF_Reg_Pay");
            entity.Property(e => e.ReviewNote).HasMaxLength(500);
            entity.Property(e => e.ReviewedAt).HasPrecision(0);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("DRAFT", "DF_Reg_Status");
            entity.Property(e => e.SubmittedAt).HasPrecision(0);
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
            entity.ToTable("RegistrationLineup");

            entity.HasIndex(e => new { e.RegistrationId, e.AccountId }, "UQ_RegistrationLineup_User").IsUnique();

            entity.Property(e => e.RegistrationLineupId).HasColumnName("RegistrationLineupID");
            entity.Property(e => e.AccountId).HasColumnName("AccountID");
            entity.Property(e => e.InGameNameSnapshot).HasMaxLength(150);
            entity.Property(e => e.PlayerCredentialId).HasColumnName("PlayerCredentialID");
            entity.Property(e => e.RegistrationId).HasColumnName("RegistrationID");
            entity.Property(e => e.Slot)
                .HasMaxLength(20)
                .IsUnicode(false);

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
            entity.ToTable("Team");

            entity.HasIndex(e => new { e.GameId, e.Name }, "UQ_Team_Name").IsUnique();

            entity.HasIndex(e => new { e.GameId, e.Tag }, "UQ_Team_Tag").IsUnique();

            entity.HasIndex(e => e.InviteCode, "UX_Team_InviteCode")
                .IsUnique()
                .HasFilter("([InviteCode] IS NOT NULL)");

            entity.Property(e => e.TeamId).HasColumnName("TeamID");
            entity.Property(e => e.CaptainAccountId).HasColumnName("CaptainAccountID");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_Team_At");
            entity.Property(e => e.GameId).HasColumnName("GameID");
            entity.Property(e => e.InviteCode)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.InviteExpiresAt).HasPrecision(0);
            entity.Property(e => e.LogoUrl).HasMaxLength(500);
            entity.Property(e => e.Name).HasMaxLength(150);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("ACTIVE", "DF_Team_Status");
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
            entity.ToTable("TeamMember");

            entity.HasIndex(e => new { e.TeamId, e.AccountId }, "UX_TeamMember_ActiveInTeam")
                .IsUnique()
                .HasFilter("([Status]='ACTIVE')");

            entity.HasIndex(e => new { e.AccountId, e.GameId }, "UX_TeamMember_OneTeamPerGame")
                .IsUnique()
                .HasFilter("([Status]='ACTIVE')");

            entity.Property(e => e.TeamMemberId).HasColumnName("TeamMemberID");
            entity.Property(e => e.AccountId).HasColumnName("AccountID");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_TeamMember_At");
            entity.Property(e => e.Direction)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.GameId).HasColumnName("GameID");
            entity.Property(e => e.JoinedAt).HasPrecision(0);
            entity.Property(e => e.LeftAt).HasPrecision(0);
            entity.Property(e => e.Message).HasMaxLength(500);
            entity.Property(e => e.RecruitmentPostId).HasColumnName("RecruitmentPostID");
            entity.Property(e => e.Slot)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("STARTER", "DF_TeamMember_Slot");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("PENDING", "DF_TeamMember_Status");
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
            entity.ToTable("Tournament");

            entity.Property(e => e.TournamentId).HasColumnName("TournamentID");
            entity.Property(e => e.BankAccountInfo).HasMaxLength(300);
            entity.Property(e => e.BannerUrl).HasMaxLength(500);
            entity.Property(e => e.BestOfPerRound).HasMaxLength(200);
            entity.Property(e => e.CancelReason).HasMaxLength(500);
            entity.Property(e => e.ContactInfo).HasMaxLength(300);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_Tour_At");
            entity.Property(e => e.EndAt).HasPrecision(0);
            entity.Property(e => e.EntryFee).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Format)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.GameId).HasColumnName("GameID");
            entity.Property(e => e.IsOnline).HasDefaultValue(true, "DF_Tour_Online");
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.OrganizerAccountId).HasColumnName("OrganizerAccountID");
            entity.Property(e => e.OrganizingUnit).HasMaxLength(200);
            entity.Property(e => e.RegCloseAt).HasPrecision(0);
            entity.Property(e => e.RegOpenAt).HasPrecision(0);
            entity.Property(e => e.Region).HasMaxLength(100);
            entity.Property(e => e.RequireInGameName).HasDefaultValue(true, "DF_Tour_ReqIgn");
            entity.Property(e => e.RequirePaymentProof).HasDefaultValue(true, "DF_Tour_ReqPay");
            entity.Property(e => e.RequireStudentCard).HasDefaultValue(true, "DF_Tour_ReqCard");
            entity.Property(e => e.RulebookVersion).HasDefaultValue(1, "DF_Tour_RbVer");
            entity.Property(e => e.StartAt).HasPrecision(0);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("DRAFT", "DF_Tour_Status");
            entity.Property(e => e.StreamUrl).HasMaxLength(500);
            entity.Property(e => e.TieBreakerOrder).HasMaxLength(200);
            entity.Property(e => e.Venue).HasMaxLength(300);

            entity.HasOne(d => d.Game).WithMany(p => p.Tournaments)
                .HasForeignKey(d => d.GameId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Tournament_Game");

            entity.HasOne(d => d.OrganizerAccount).WithMany(p => p.Tournaments)
                .HasForeignKey(d => d.OrganizerAccountId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Tournament_Organizer");
        });

        modelBuilder.Entity<TournamentFollow>(entity =>
        {
            entity.ToTable("TournamentFollow");

            entity.HasIndex(e => new { e.AccountId, e.TournamentId }, "UQ_TournamentFollow").IsUnique();

            entity.Property(e => e.TournamentFollowId).HasColumnName("TournamentFollowID");
            entity.Property(e => e.AccountId).HasColumnName("AccountID");
            entity.Property(e => e.FollowedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_TourFollow_At");
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
            entity.ToTable("TournamentSponsor");

            entity.HasIndex(e => new { e.TournamentId, e.Name }, "UQ_TournamentSponsor_Name").IsUnique();

            entity.Property(e => e.TournamentSponsorId).HasColumnName("TournamentSponsorID");
            entity.Property(e => e.ContactInfo).HasMaxLength(300);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_TourSponsor_At");
            entity.Property(e => e.CreatedByAccountId).HasColumnName("CreatedByAccountID");
            entity.Property(e => e.DisplayOrder).HasDefaultValue(1, "DF_TourSponsor_Order");
            entity.Property(e => e.LogoUrl).HasMaxLength(500);
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.Tier)
                .HasMaxLength(20)
                .IsUnicode(false);
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

            entity.Property(e => e.GroupLabel).HasMaxLength(10);
            entity.Property(e => e.TeamId).HasColumnName("TeamID");
            entity.Property(e => e.TournamentId).HasColumnName("TournamentID");
        });

        modelBuilder.Entity<VwLeaderboard>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_Leaderboard");

            entity.Property(e => e.GameId).HasColumnName("GameID");
            entity.Property(e => e.TeamId).HasColumnName("TeamID");
        });

        modelBuilder.Entity<VwTeamPenaltyPoint>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_TeamPenaltyPoint");

            entity.Property(e => e.TeamId).HasColumnName("TeamID");
            entity.Property(e => e.TournamentId).HasColumnName("TournamentID");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
