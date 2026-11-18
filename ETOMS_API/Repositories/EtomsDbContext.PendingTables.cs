using ETOMS_API.Models;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Repositories;

// Phần cấu hình KHÔNG sinh ra khi scaffold lại EtomsDbContext.cs (scaffold ghi đè file đó, file này giữ nguyên).
// 18/11/2026: Hải scaffold lại context có đủ GameRuleset/GameMap/GamePosition/TournamentRuleset/MatchPickBan và
// cột RegistrationLineup.GamePositionID, nên phần ánh xạ tạm các bảng đó đã bỏ khỏi file này (trùng => không build được).
public partial class EtomsDbContext
{
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

        // Cột bool NOT NULL có DEFAULT 1 trong DB: model giữ kiểu bool (scaffold mới sinh bool? làm vỡ ~30 chỗ code),
        // và EF PHẢI luôn ghi giá trị thật. Nếu để EF coi là "giá trị do DB sinh", khi giá trị là false EF bỏ cột khỏi
        // INSERT và DB tự điền 1 => tạo giải "thi đấu trực tiếp" thành online, bỏ yêu cầu IGN/biên lai/giấy tờ thành bắt buộc.
        modelBuilder.Entity<Tournament>(entity =>
        {
            entity.Property(e => e.IsOnline).ValueGeneratedNever();
            entity.Property(e => e.RequireInGameName).ValueGeneratedNever();
            entity.Property(e => e.RequirePaymentProof).ValueGeneratedNever();
            entity.Property(e => e.RequireIdentityDocument).ValueGeneratedNever();
        });
        modelBuilder.Entity<PaymentRecord>(entity => entity.Property(e => e.IsCurrent).ValueGeneratedNever());

        // Giới hạn 35 bảng: yêu cầu thay người (MAT-05/06) dùng MatchRequest; cột ghi chú khi duyệt thêm 18/11.
        // DB chung chưa chạy script thêm cột thì bỏ qua cột (xem PendingSchema) để MatchRequest vẫn đọc được.
        modelBuilder.Entity<MatchRequest>(entity =>
        {
            if (PendingSchema.MatchRequestDecisionNote) entity.Property(e => e.DecisionNote).HasMaxLength(300);
            else entity.Ignore(e => e.DecisionNote);
        });

        // ADM thông báo hệ thống + banner (code của Hải dùng _db.Set<...>()). Hai bảng bị bỏ nhầm 17/11 và khôi phục 18/11,
        // nên bản scaffold lại của context không có — ánh xạ ở đây theo đúng lược đồ DB chung.
        modelBuilder.Entity<SystemAnnouncement>(entity =>
        {
            entity.ToTable("SystemAnnouncement");
            entity.HasIndex(e => e.SentAt, "IX_SystemAnnouncement_SentAt");
            entity.Property(e => e.SystemAnnouncementId).HasColumnName("SystemAnnouncementID");
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.Body).HasMaxLength(1000);
            entity.Property(e => e.LinkUrl).HasMaxLength(500);
            entity.Property(e => e.TargetRoles).HasMaxLength(60);
            entity.Property(e => e.Channel).HasMaxLength(20);
            entity.Property(e => e.SentByAccountId).HasColumnName("SentByAccountID");
            entity.HasOne(d => d.SentByAccount).WithMany()
                .HasForeignKey(d => d.SentByAccountId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SystemAnnouncement_By");
        });
        modelBuilder.Entity<SystemBanner>(entity =>
        {
            entity.ToTable("SystemBanner");
            entity.HasIndex(e => new { e.IsActive, e.StartAt, e.EndAt }, "IX_SystemBanner_Live");
            entity.Property(e => e.SystemBannerId).HasColumnName("SystemBannerID");
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.ImageUrl).HasMaxLength(500);
            entity.Property(e => e.LinkUrl).HasMaxLength(500);
            entity.Property(e => e.BannerType).HasMaxLength(20);
            entity.Property(e => e.CreatedByAccountId).HasColumnName("CreatedByAccountID");
            entity.HasOne(d => d.CreatedByAccount).WithMany()
                .HasForeignKey(d => d.CreatedByAccountId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SystemBanner_By");
        });
        modelBuilder.Entity<TournamentSponsor>(entity => entity.Property(e => e.ShowWhenCancelled).ValueGeneratedNever());
        modelBuilder.Entity<Account>(entity =>
        {
            entity.Property(e => e.NotifyInApp).ValueGeneratedNever();
            entity.Property(e => e.NotifyEmail).ValueGeneratedNever();
        });
    }
}
