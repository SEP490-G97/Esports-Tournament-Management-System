using ETOMS.Application.Interfaces;
using ETOMS.Infrastructure.Data;

namespace ETOMS.Infrastructure.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly EtomsDbContext _context;
    private readonly Dictionary<Type, object> _repositories = new();

    public UnitOfWork(EtomsDbContext context) => _context = context;

    public IGenericRepository<TEntity> Repository<TEntity>() where TEntity : class
    {
        if (!_repositories.TryGetValue(typeof(TEntity), out var repo))
        {
            repo = new GenericRepository<TEntity>(_context);
            _repositories[typeof(TEntity)] = repo;
        }
        return (IGenericRepository<TEntity>)repo;
    }

    public Task<int> SaveChangesAsync() => _context.SaveChangesAsync();

    public void Dispose() => _context.Dispose();
}
