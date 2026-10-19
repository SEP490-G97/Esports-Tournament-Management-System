namespace ETOMS.Application.Interfaces;

// Gom nhiều thao tác repository vào 1 lần lưu (1 transaction). Lấy repository qua Repository<T>()
// rồi gọi SaveChangesAsync() một lần ở cuối.
public interface IUnitOfWork : IDisposable
{
    IGenericRepository<TEntity> Repository<TEntity>() where TEntity : class;
    Task<int> SaveChangesAsync();
}
