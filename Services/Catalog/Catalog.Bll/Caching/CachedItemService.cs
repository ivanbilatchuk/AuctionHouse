using Catalog.Bll.DTO;
using Catalog.Bll.Services;

namespace Catalog.Bll.Caching;

// Декоратор IItemService: cache-aside для картки лота і синхронна інвалідація після кожного запису.
// Сторінкові списки не кешуються (багато комбінацій фільтрів), їх швидкість забезпечують індекси БД.
public sealed class CachedItemService(ItemService inner, TwoLevelCache cache) : IItemService
{
    public Task<PagedResult<ItemDto>> GetListAsync(ItemQuery query, CancellationToken ct = default)
        => inner.GetListAsync(query, ct);

    public Task<PagedResult<ItemDto>> GetBySellerAsync(Guid sellerExternalId, PageRequest request, CancellationToken ct = default)
        => inner.GetBySellerAsync(sellerExternalId, request, ct);

    public Task<ItemDetailsDto> GetByIdAsync(long id, CancellationToken ct = default)
        => cache.GetOrCreateAsync(CatalogCacheKeys.Item(id), token => inner.GetByIdAsync(id, token), CatalogCacheKeys.ItemSettings, ct);

    public Task<ItemDetailsDto> CreateAsync(CreateItemDto input, CancellationToken ct = default)
        => inner.CreateAsync(input, ct);

    public async Task UpdateAsync(long id, UpdateItemDto input, CancellationToken ct = default)
    {
        await inner.UpdateAsync(id, input, ct);
        await cache.InvalidateAsync(CatalogCacheKeys.Item(id), "item updated", ct);
    }

    public async Task ChangeStatusAsync(long id, ChangeItemStatusDto input, CancellationToken ct = default)
    {
        await inner.ChangeStatusAsync(id, input, ct);
        await cache.InvalidateAsync(CatalogCacheKeys.Item(id), $"item status changed to {input.Status}", ct);
    }

    public async Task AddTagAsync(long id, long tagId, CancellationToken ct = default)
    {
        await inner.AddTagAsync(id, tagId, ct);
        await cache.InvalidateAsync(CatalogCacheKeys.Item(id), $"tag {tagId} added", ct);
    }

    public async Task RemoveTagAsync(long id, long tagId, CancellationToken ct = default)
    {
        await inner.RemoveTagAsync(id, tagId, ct);
        await cache.InvalidateAsync(CatalogCacheKeys.Item(id), $"tag {tagId} removed", ct);
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        await inner.DeleteAsync(id, ct);
        await cache.InvalidateAsync(CatalogCacheKeys.Item(id), "item deleted", ct);
    }
}
