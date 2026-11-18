using System.Linq.Expressions;

namespace ETOMS_API.Repositories.Interface;

// Repository chung cho mọi entity. Service inject cái này để CRUD, không viết SQL/EF thẳng.
// Gộp từ các nhánh: bộ hàm bất đồng bộ (Dũng, main-dev) và bộ hàm đồng bộ (Hải).
public interface IGenericRepository<TEntity> where TEntity : class
{
    Task<TEntity?> GetByIdAsync(object id);
    Task<IReadOnlyList<TEntity>> GetAllAsync();

    // Kiểm tra có bản ghi nào thỏa điều kiện không (dùng để check trùng tên/tag...).
    Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate);

    // Trả IQueryable để Service tự Include/Where/sắp xếp cho các màn hình phức tạp.
    IQueryable<TEntity> Query();

    Task AddAsync(TEntity entity);
    void Update(TEntity entity);
    void Remove(TEntity entity);

    // Bản đồng bộ (dùng ở module đăng nhập).
    TEntity? GetById(object id);
    void Add(TEntity entity);
    void Delete(TEntity entity);
}
