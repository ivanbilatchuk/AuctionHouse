using Catalog.Domain.Entities;

namespace Catalog.Dal.Interfaces;

public interface IItemTagRepository : IGenericRepository<ItemTag>
{
    Task<IReadOnlyList<ItemTag>> GetRecentlyTaggedAsync(string tagSlug, DateTime since, CancellationToken ct = default);
}
