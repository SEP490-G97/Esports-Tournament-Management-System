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

    public virtual DbSet<MatchSubstitution> MatchSubstitutions { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseMySQL("Server=15.135.76.150;Port=3306;Database=etoms_db;User=etoms;Password=etoms;");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MatchSubstitution>(entity =>
        {
            entity.HasKey(e => e.MatchSubstitutionId).HasName("PRIMARY");

            entity.ToTable("MatchSubstitution");

            entity.HasIndex(e => e.DecidedByAccountId, "FK_MatchSubstitution_DecBy");

            entity.HasIndex(e => e.InAccountId, "FK_MatchSubstitution_In");

            entity.HasIndex(e => e.OutAccountId, "FK_MatchSubstitution_Out");

            entity.HasIndex(e => e.RequestedByAccountId, "FK_MatchSubstitution_ReqBy");

            entity.HasIndex(e => e.TeamId, "FK_MatchSubstitution_Team");

            entity.HasIndex(e => new { e.MatchId, e.TeamId, e.Status }, "IX_MatchSubstitution_Match");

            entity.Property(e => e.MatchSubstitutionId).HasColumnName("MatchSubstitutionID");
            entity.Property(e => e.DecidedAt).HasColumnType("datetime");
            entity.Property(e => e.DecidedByAccountId).HasColumnName("DecidedByAccountID");
            entity.Property(e => e.DecisionNote).HasMaxLength(300);
            entity.Property(e => e.InAccountId)
                .HasComment("Dự bị vào sân")
                .HasColumnName("InAccountID");
            entity.Property(e => e.MatchId).HasColumnName("MatchID");
            entity.Property(e => e.OutAccountId)
                .HasComment("Tuyển thủ chính bị thay ra")
                .HasColumnName("OutAccountID");
            entity.Property(e => e.Reason).HasMaxLength(300);
            entity.Property(e => e.RequestedAt)
                .HasDefaultValueSql("'utc_timestamp()'")
                .HasColumnType("datetime");
            entity.Property(e => e.RequestedByAccountId).HasColumnName("RequestedByAccountID");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValueSql("'PENDING'");
            entity.Property(e => e.TeamId).HasColumnName("TeamID");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
