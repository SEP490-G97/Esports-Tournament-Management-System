using ETOMS_API.Models;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Repositories;

// Ánh xạ các bảng và cột code đã dùng nhưng CHƯA có trong database chung:
//   bảng GameRuleset (GRL-01), GameMap (GRL-02), GamePosition (GRL-03), TournamentRuleset (GRL-06),
//   MatchPickBan (GRL-07), cột RegistrationLineup.GamePositionID (GRL-04).
// Tách khỏi file scaffold để không đụng EtomsDbContext.cs.
// Khi nhóm tạo bảng, thêm cột và scaffold lại context: xóa file này và Models/RegistrationLineup.Position.cs.
public partial class EtomsDbContext
{
    public virtual DbSet<GameRuleset> GameRulesets { get; set; }

    public virtual DbSet<GameMap> GameMaps { get; set; }

    public virtual DbSet<GamePosition> GamePositions { get; set; }

    public virtual DbSet<TournamentRuleset> TournamentRulesets { get; set; }

    public virtual DbSet<MatchPickBan> MatchPickBans { get; set; }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        // MySql.EntityFrameworkCore (provider Oracle chính thức) đọc cột DATE ra DateTime ở tầng
        // thấp, không tự chuyển được sang DateOnly như Pomelo — thiếu dòng này thì bất kỳ tài khoản
        // nào có DateOfBirth khác NULL sẽ làm /auth/login và /auth/register crash 500
        // (InvalidCastException: DateTime -> DateOnly) ngay khi EF đọc lại dòng Account đó.
        modelBuilder.Entity<Account>(entity =>
        {
            entity.Property(e => e.DateOfBirth).HasConversion(
                d => d.HasValue ? d.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null,
                d => d.HasValue ? DateOnly.FromDateTime(d.Value) : (DateOnly?)null);
        });

        modelBuilder.Entity<GameRuleset>(entity =>
        {
            entity.ToTable("GameRuleset");

            entity.HasIndex(e => new { e.GameId, e.Name }, "UQ_GameRuleset_Name").IsUnique();

            entity.Property(e => e.GameRulesetId).HasColumnName("GameRulesetID");
            entity.Property(e => e.GameId).HasColumnName("GameID");
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.DefaultFormat).HasMaxLength(30).IsUnicode(false);
            entity.Property(e => e.AllowedEvidenceTypes).HasMaxLength(200);
            entity.Property(e => e.MaxEvidenceFileSizeMb).HasColumnName("MaxEvidenceFileSizeMB");
            entity.Property(e => e.Status).HasMaxLength(20).HasDefaultValue("ACTIVE");

            entity.HasOne(d => d.Game).WithMany()
                .HasForeignKey(d => d.GameId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_GameRuleset_Game");
        });

        modelBuilder.Entity<GameMap>(entity =>
        {
            entity.ToTable("GameMap");

            entity.HasIndex(e => new { e.GameRulesetId, e.Name }, "UQ_GameMap_Name").IsUnique();

            entity.Property(e => e.GameMapId).HasColumnName("GameMapID");
            entity.Property(e => e.GameRulesetId).HasColumnName("GameRulesetID");
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.ImageUrl).HasMaxLength(500);
            entity.Property(e => e.Status).HasMaxLength(20).HasDefaultValue("ACTIVE");

            entity.HasOne(d => d.GameRuleset).WithMany()
                .HasForeignKey(d => d.GameRulesetId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_GameMap_Ruleset");
        });

        modelBuilder.Entity<GamePosition>(entity =>
        {
            entity.ToTable("GamePosition");

            entity.HasIndex(e => new { e.GameRulesetId, e.Name }, "UQ_GamePosition_Name").IsUnique();

            entity.Property(e => e.GamePositionId).HasColumnName("GamePositionID");
            entity.Property(e => e.GameRulesetId).HasColumnName("GameRulesetID");
            entity.Property(e => e.Name).HasMaxLength(50);
            entity.Property(e => e.Status).HasMaxLength(20).HasDefaultValue("ACTIVE");

            entity.HasOne(d => d.GameRuleset).WithMany()
                .HasForeignKey(d => d.GameRulesetId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_GamePosition_Ruleset");
        });

        modelBuilder.Entity<TournamentRuleset>(entity =>
        {
            entity.ToTable("TournamentRuleset");

            entity.HasIndex(e => e.TournamentId, "UQ_TournamentRuleset_Tournament").IsUnique();

            entity.Property(e => e.TournamentRulesetId).HasColumnName("TournamentRulesetID");
            entity.Property(e => e.TournamentId).HasColumnName("TournamentID");
            entity.Property(e => e.GameRulesetId).HasColumnName("GameRulesetID");

            entity.HasOne(d => d.Tournament).WithMany()
                .HasForeignKey(d => d.TournamentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TournamentRuleset_Tournament");

            entity.HasOne(d => d.GameRuleset).WithMany()
                .HasForeignKey(d => d.GameRulesetId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TournamentRuleset_Ruleset");
        });

        modelBuilder.Entity<MatchPickBan>(entity =>
        {
            entity.ToTable("MatchPickBan");

            entity.HasIndex(e => new { e.MatchId, e.StepNo }, "UQ_MatchPickBan_Step").IsUnique();

            entity.Property(e => e.MatchPickBanId).HasColumnName("MatchPickBanID");
            entity.Property(e => e.MatchId).HasColumnName("MatchID");
            entity.Property(e => e.Action).HasMaxLength(10).IsUnicode(false);
            entity.Property(e => e.TeamId).HasColumnName("TeamID");
            entity.Property(e => e.GameMapId).HasColumnName("GameMapID");
            entity.Property(e => e.RecordedByAccountId).HasColumnName("RecordedByAccountID");

            entity.HasOne(d => d.Match).WithMany()
                .HasForeignKey(d => d.MatchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MatchPickBan_Match");

            entity.HasOne(d => d.Team).WithMany()
                .HasForeignKey(d => d.TeamId)
                .HasConstraintName("FK_MatchPickBan_Team");

            entity.HasOne(d => d.GameMap).WithMany()
                .HasForeignKey(d => d.GameMapId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MatchPickBan_Map");

            entity.HasOne(d => d.RecordedByAccount).WithMany()
                .HasForeignKey(d => d.RecordedByAccountId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MatchPickBan_Account");
        });

        // Cột mới trên bảng có sẵn: khi database chưa có cột, MỌI truy vấn đọc entity RegistrationLineup
        // sẽ lỗi "Unknown column". Nhóm đã chọn cách này thay vì tạo bảng riêng.
        // Chưa có cột (PendingSchema dò lúc khởi động) thì bỏ qua để không làm hỏng đăng ký giải/đội hình.
        modelBuilder.Entity<RegistrationLineup>(entity =>
        {
            if (!PendingSchema.LineupGamePosition)
            {
                entity.Ignore(e => e.GamePosition);
                entity.Ignore(e => e.GamePositionId);
                return;
            }

            entity.Property(e => e.GamePositionId).HasColumnName("GamePositionID");

            entity.HasOne(d => d.GamePosition).WithMany()
                .HasForeignKey(d => d.GamePositionId)
                .HasConstraintName("FK_RegistrationLineup_Position");
        });
    }
}
