namespace ETOMS.Application.Interfaces;

// Repository chung cho mọi entity. Service inject cái này để CRUD, không viết SQL/EF thẳng.
public interface IGenericRepository<TEntity> where TEntity : class
{
    Task<TEntity?> GetByIdAsync(object id);
    Task<IReadOnlyList<TEntity>> GetAllAsync();
    Task AddAsync(TEntity entity);
    void Update(TEntity entity);
    void Remove(TEntity entity);
}
