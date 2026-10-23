using Ardalis.Specification;

namespace Catalog.Dal.Interfaces;

public interface IGenericRepository<T> where T : class
{
    Task<T?> GetByIdAsync(long id, CancellationToken ct = default);

    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default);

    Task AddAsync(T entity, CancellationToken ct = default);

    void Update(T entity);

    void Delete(T entity);

    Task<IReadOnlyList<T>> ListAsync(ISpecification<T> specification, CancellationToken ct = default);

    Task<T?> FirstOrDefaultAsync(ISpecification<T> specification, CancellationToken ct = default);

    Task<int> CountAsync(ISpecification<T> specification, CancellationToken ct = default);
}
