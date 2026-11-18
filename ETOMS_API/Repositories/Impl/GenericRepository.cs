using System.Linq.Expressions;
using ETOMS_API.Repositories.Interface;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Repositories.Impl;

public class GenericRepository<TEntity> : IGenericRepository<TEntity> where TEntity : class
{
    private readonly DbSet<TEntity> _dbSet;

    public GenericRepository(EtomsDbContext context) => _dbSet = context.Set<TEntity>();

    public GenericRepository(DbSet<TEntity> dbSet) => _dbSet = dbSet;

    public async Task<TEntity?> GetByIdAsync(object id) => await _dbSet.FindAsync(id);

    public async Task<IReadOnlyList<TEntity>> GetAllAsync() => await _dbSet.ToListAsync();

    public Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate) => _dbSet.AnyAsync(predicate);

    public IQueryable<TEntity> Query() => _dbSet.AsQueryable();

    public async Task AddAsync(TEntity entity) => await _dbSet.AddAsync(entity);

    public void Update(TEntity entity) => _dbSet.Update(entity);

    public void Remove(TEntity entity) => _dbSet.Remove(entity);

    public TEntity? GetById(object id) => _dbSet.Find(id);

    public void Add(TEntity entity) => _dbSet.Add(entity);

    public void Delete(TEntity entity) => _dbSet.Remove(entity);
}
