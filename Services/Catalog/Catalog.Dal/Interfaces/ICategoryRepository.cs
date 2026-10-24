using Catalog.Domain.Entities;

namespace Catalog.Dal.Interfaces;

public interface ICategoryRepository : IGenericRepository<Category>
{
    Task<Category?> GetWithItemsAsync(long id, CancellationToken ct = default);

    Task<bool> ExistsAsync(long id, CancellationToken ct = default);

    Task<bool> ExistsBySlugAsync(string slug, CancellationToken ct = default);

    Task<bool> HasDependentsAsync(long id, CancellationToken ct = default);
}
