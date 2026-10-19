using Microsoft.EntityFrameworkCore;

namespace ETOMS.Infrastructure.Repositories;

public class GenericRepository<TEntity> : IGenericRepository<TEntity> where TEntity : class
{
    private readonly DbContext _context;
    private readonly DbSet<TEntity> _dbSet;

    public GenericRepository(DbContext context)
    {
        _context = context;
        _dbSet = context.Set<TEntity>();
    }

    public async Task<TEntity?> GetByIdAsync(object id) => await _dbSet.FindAsync(id);

    public async Task<IReadOnlyList<TEntity>> GetAllAsync() => await _dbSet.ToListAsync();

    public async Task AddAsync(TEntity entity) => await _dbSet.AddAsync(entity);

    public void Update(TEntity entity) => _dbSet.Update(entity);

    public void Remove(TEntity entity) => _dbSet.Remove(entity);

    public Task<int> SaveChangesAsync() => _context.SaveChangesAsync();
}
