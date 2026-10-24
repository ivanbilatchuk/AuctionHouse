using Catalog.Domain.Entities;

namespace Catalog.Dal.Interfaces;

public interface IUnitOfWork
{
    IItemRepository Items { get; }

    ICategoryRepository Categories { get; }

    IItemTagRepository ItemTags { get; }

    IGenericRepository<Tag> Tags { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
