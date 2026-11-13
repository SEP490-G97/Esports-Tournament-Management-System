using System.Data;
using ETOMS_API.Repositories.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ETOMS_API.Repositories.Impl;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly EtomsDbContext _context;
    private readonly Dictionary<Type, object> _repositories = new();

    public UnitOfWork(EtomsDbContext context) => _context = context;

    // Mỗi entity một repository, dùng lại trong cùng request.
    public IGenericRepository<TEntity> Repository<TEntity>() where TEntity : class
    {
        if (!_repositories.TryGetValue(typeof(TEntity), out var repo))
        {
            repo = new GenericRepository<TEntity>(_context);
            _repositories[typeof(TEntity)] = repo;
        }
        return (IGenericRepository<TEntity>)repo;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => _context.SaveChangesAsync(cancellationToken);

    public Task<IDbContextTransaction> BeginTransactionAsync(
        IsolationLevel isolationLevel,
        CancellationToken cancellationToken = default)
        => _context.Database.BeginTransactionAsync(isolationLevel, cancellationToken);

    public void Dispose() => _context.Dispose();
}
