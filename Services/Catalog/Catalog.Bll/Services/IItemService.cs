using Catalog.Bll.DTO;

namespace Catalog.Bll.Services;

public interface IItemService
{
    Task<PagedResult<ItemDto>> GetListAsync(ItemQuery query, CancellationToken ct = default);

    Task<PagedResult<ItemDto>> GetBySellerAsync(Guid sellerExternalId, PageRequest request, CancellationToken ct = default);

    Task<ItemDetailsDto> GetByIdAsync(long id, CancellationToken ct = default);

    Task<ItemDetailsDto> CreateAsync(CreateItemDto input, CancellationToken ct = default);

    Task UpdateAsync(long id, UpdateItemDto input, CancellationToken ct = default);

    Task ChangeStatusAsync(long id, ChangeItemStatusDto input, CancellationToken ct = default);

    Task AddTagAsync(long id, long tagId, CancellationToken ct = default);

    Task RemoveTagAsync(long id, long tagId, CancellationToken ct = default);

    Task DeleteAsync(long id, CancellationToken ct = default);
}
