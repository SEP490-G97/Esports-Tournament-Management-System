using ETOMS_API.Models;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Repositories;

// Ánh xạ các bảng code đã dùng nhưng CHƯA có trong database chung: GameMap (GRL-02),
// GamePosition (GRL-03). Tách khỏi file scaffold để không đụng EtomsDbContext.cs.
// Khi nhóm tạo bảng và scaffold lại context: xóa file này.
public partial class EtomsDbContext
{
    public virtual DbSet<GameMap> GameMaps { get; set; }

    public virtual DbSet<GamePosition> GamePositions { get; set; }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GameMap>(entity =>
        {
            entity.ToTable("GameMap");

            entity.HasIndex(e => new { e.GameId, e.Name }, "UQ_GameMap_Name").IsUnique();

            entity.Property(e => e.GameMapId).HasColumnName("GameMapID");
            entity.Property(e => e.GameId).HasColumnName("GameID");
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.ImageUrl).HasMaxLength(500);
            entity.Property(e => e.Status).HasMaxLength(20).HasDefaultValue("ACTIVE");

            entity.HasOne(d => d.Game).WithMany()
                .HasForeignKey(d => d.GameId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_GameMap_Game");
        });

        modelBuilder.Entity<GamePosition>(entity =>
        {
            entity.ToTable("GamePosition");

            entity.HasIndex(e => new { e.GameId, e.Name }, "UQ_GamePosition_Name").IsUnique();

            entity.Property(e => e.GamePositionId).HasColumnName("GamePositionID");
            entity.Property(e => e.GameId).HasColumnName("GameID");
            entity.Property(e => e.Name).HasMaxLength(50);
            entity.Property(e => e.Status).HasMaxLength(20).HasDefaultValue("ACTIVE");

            entity.HasOne(d => d.Game).WithMany()
                .HasForeignKey(d => d.GameId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_GamePosition_Game");
        });
    }
}
