using ETOMS_API.Models;
using ETOMS_API.Repositories.Interface;

namespace ETOMS_API.Repositories.Impl;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly EtomsDbContext _dbContext;

    public UnitOfWork(EtomsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public IGenericRepository<TEntity> Repository<TEntity>() where TEntity : class
        => new GenericRepository<TEntity>(_dbContext.Set<TEntity>());

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => _dbContext.SaveChangesAsync(cancellationToken);

    public void Dispose() => _dbContext.Dispose();
}
