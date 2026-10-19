using Microsoft.EntityFrameworkCore;

namespace ETOMS.Infrastructure.Data;

// DbContext kết nối SQL Server. HIỆN ĐANG RỖNG.
// Khi đã tạo database ETOMS (xem CLAUDE.md mục 6), chạy lệnh scaffold để sinh 62 entity
// vào Domain/Entities và sinh lại class này kèm các DbSet. KHÔNG dùng migrations để đổi lược đồ.
public class EtomsDbContext : DbContext
{
    public EtomsDbContext(DbContextOptions<EtomsDbContext> options) : base(options)
    {
    }
}
