using Catalog.Domain.Entities;

namespace Catalog.Dal.Interfaces;

public interface IItemRepository : IGenericRepository<Item>
{
    Task<Item?> GetWithDetailsAsync(long id, bool asTracking = false, CancellationToken ct = default);

    Task<bool> ExistsBySkuAsync(string sku, CancellationToken ct = default);

    Task<bool> ExistsBySlugAsync(string slug, long? exceptItemId = null, CancellationToken ct = default);
}
