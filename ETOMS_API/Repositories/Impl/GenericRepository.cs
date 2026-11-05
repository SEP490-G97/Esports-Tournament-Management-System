using ETOMS_API.Repositories.Interface;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Repositories.Impl
{
    public class GenericRepository<T> : ETOMS_API.Repositories.Interface.IGenericRepository<T> where T : class
    {
        private readonly DbSet<T> _entities;

        public GenericRepository(DbSet<T> entities)
        {
            _entities = entities;
        }

        public IQueryable<T> Query() => _entities;

        public T? GetById(object id) => _entities.Find(id);

        public void Add(T entity)
        {
            _entities.Add(entity);
        }

        public void Delete(T entity)
        {
            _entities.Remove(entity);
        }

        public void Update(T entity)
        {
            _entities.Update(entity);
        }
    }
    }
