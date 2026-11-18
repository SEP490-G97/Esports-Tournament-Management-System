using System.Data;
using Microsoft.EntityFrameworkCore.Storage;

namespace ETOMS_API.Repositories.Interface;

// Gom nhiều thao tác repository vào 1 lần lưu (1 transaction). Lấy repository qua Repository<T>()
// rồi gọi SaveChangesAsync() một lần ở cuối. Cần transaction rõ ràng (vd hàng đợi đăng ký) thì
// dùng BeginTransactionAsync.
public interface IUnitOfWork : IDisposable
{
    IGenericRepository<TEntity> Repository<TEntity>() where TEntity : class;

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    Task<IDbContextTransaction> BeginTransactionAsync(
        IsolationLevel isolationLevel,
        CancellationToken cancellationToken = default);
}
