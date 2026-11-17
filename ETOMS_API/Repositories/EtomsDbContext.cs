using System;
using System.Collections.Generic;
using ETOMS_API.Models;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Repositories;

public partial class EtomsDbContext : DbContext
{
    public EtomsDbContext()
    {
    }

    public EtomsDbContext(DbContextOptions<EtomsDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<MatchChatMessage> MatchChatMessages { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseMySQL("Server=15.135.76.150;Port=3306;Database=etoms_db;User=etoms;Password=etoms;");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MatchChatMessage>(entity =>
        {
            entity.HasKey(e => e.MatchChatMessageId).HasName("PRIMARY");

            entity.ToTable("MatchChatMessage");

            entity.HasIndex(e => e.TeamId, "FK_MatchChatMessage_Team");

            entity.HasIndex(e => new { e.MatchId, e.MatchChatMessageId }, "IX_MatchChatMessage_Match");

            entity.HasIndex(e => new { e.SenderAccountId, e.SentAt }, "IX_MatchChatMessage_Sender");

            entity.Property(e => e.MatchChatMessageId).HasColumnName("MatchChatMessageID");
            entity.Property(e => e.Body).HasMaxLength(1000);
            entity.Property(e => e.MatchId).HasColumnName("MatchID");
            entity.Property(e => e.SenderAccountId).HasColumnName("SenderAccountID");
            entity.Property(e => e.SenderRole)
                .HasMaxLength(20)
                .HasComment("Vai trò tại thời điểm gửi: CAPTAIN / ORGANIZER / REFEREE");
            entity.Property(e => e.SentAt)
                .HasDefaultValueSql("'utc_timestamp(3)'")
                .HasColumnType("datetime(3)");
            entity.Property(e => e.TeamId)
                .HasComment("Đội của đội trưởng (NULL với BTC / trọng tài)")
                .HasColumnName("TeamID");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
