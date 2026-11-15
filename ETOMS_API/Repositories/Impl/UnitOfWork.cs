namespace ETOMS_API.Repositories.Impl;

public sealed class UnitOfWork : ETOMS_API.Repositories.Interface.IUnitOfWork
{
    private readonly ETOMS_API.Repositories.EtomsDbContext _dbContext;

    public UnitOfWork(ETOMS_API.Repositories.EtomsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public ETOMS_API.Repositories.Interface.IGenericRepository<TEntity> Repository<TEntity>() where TEntity : class
        => new GenericRepository<TEntity>(_dbContext.Set<TEntity>());

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => _dbContext.SaveChangesAsync(cancellationToken);

    public void Dispose() => _dbContext.Dispose();
}
