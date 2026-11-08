using ETOMS_API.Models;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Repositories;

// GRL-02: ánh xạ bảng GameMap, tách khỏi file scaffold để không đụng EtomsDbContext.cs.
// Xóa file này khi bảng đã tạo trong database và scaffold lại context.
public partial class EtomsDbContext
{
    public virtual DbSet<GameMap> GameMaps { get; set; }

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
    }
}
