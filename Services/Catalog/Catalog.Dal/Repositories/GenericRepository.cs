using Ardalis.Specification;
using Ardalis.Specification.EntityFrameworkCore;
using Catalog.Dal.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Dal.Repositories;

// Репозиторій лише змінює стан сутностей у контексті; зберігає зміни UnitOfWork.SaveChangesAsync.
public class GenericRepository<T> : IGenericRepository<T> where T : class
{
    protected readonly CatalogDbContext Context;
    protected readonly DbSet<T> Set;

    public GenericRepository(CatalogDbContext context)
    {
        Context = context;
        Set = context.Set<T>();
    }

    public virtual async Task<T?> GetByIdAsync(long id, CancellationToken ct = default)
        => await Set.FindAsync(new object[] { id }, ct);

    public virtual async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default)
        => await Set.AsNoTracking().ToListAsync(ct);

    public virtual async Task AddAsync(T entity, CancellationToken ct = default)
        => await Set.AddAsync(entity, ct);

    public virtual void Update(T entity) => Set.Update(entity);

    public virtual void Delete(T entity) => Set.Remove(entity);

    public virtual async Task<IReadOnlyList<T>> ListAsync(ISpecification<T> specification, CancellationToken ct = default)
        => await Set.WithSpecification(specification).ToListAsync(ct);

    public virtual async Task<T?> FirstOrDefaultAsync(ISpecification<T> specification, CancellationToken ct = default)
        => await Set.WithSpecification(specification).FirstOrDefaultAsync(ct);

    // Лише критерії (Where) без OrderBy/Skip/Take: totalCount рахує БД по всій вибірці.
    public virtual async Task<int> CountAsync(ISpecification<T> specification, CancellationToken ct = default)
        => await SpecificationEvaluator.Default
            .GetQuery(Set.AsQueryable(), specification, evaluateCriteriaOnly: true)
            .CountAsync(ct);
}
