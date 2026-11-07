namespace ETOMS_API.Repositories.Interface
{
    public interface IGenericRepository<T> where T : class
    {
        IQueryable<T> Query();
        T? GetById(object id);
        void Add(T entity);
        void Update(T entity);
        void Delete(T entity);
    }
}
