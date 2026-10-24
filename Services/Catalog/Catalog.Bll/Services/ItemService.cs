using AutoMapper;
using Catalog.Bll.DTO;
using Catalog.Bll.Specifications;
using Catalog.Dal.Interfaces;
using Catalog.Domain.Entities;
using Catalog.Domain.Exceptions;

namespace Catalog.Bll.Services;

public sealed class ItemService(IUnitOfWork uow, IMapper mapper) : IItemService
{
    public async Task<PagedResult<ItemDto>> GetListAsync(ItemQuery query, CancellationToken ct = default)
    {
        var (page, pageSize) = Paging.Normalize(query.Page, query.PageSize);
        var spec = new ItemsFilterSpec(query, page, pageSize);

        var items = await uow.Items.ListAsync(spec, ct);
        var total = await uow.Items.CountAsync(spec, ct);

        return new PagedResult<ItemDto>(mapper.Map<List<ItemDto>>(items), total, page, pageSize);
    }

    public async Task<PagedResult<ItemDto>> GetBySellerAsync(Guid sellerExternalId, PageRequest request, CancellationToken ct = default)
    {
        var (page, pageSize) = Paging.Normalize(request.Page, request.PageSize);
        var spec = new ItemsBySellerSpec(sellerExternalId, page, pageSize);

        var items = await uow.Items.ListAsync(spec, ct);
        var total = await uow.Items.CountAsync(spec, ct);

        return new PagedResult<ItemDto>(mapper.Map<List<ItemDto>>(items), total, page, pageSize);
    }

    public async Task<ItemDetailsDto> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var item = await uow.Items.GetWithDetailsAsync(id, ct: ct)
            ?? throw new EntityNotFoundException(nameof(Item), id);

        return mapper.Map<ItemDetailsDto>(item);
    }

    public async Task<ItemDetailsDto> CreateAsync(CreateItemDto input, CancellationToken ct = default)
    {
        var sku = input.Sku.Trim().ToUpperInvariant();
        var slug = input.Slug.Trim().ToLowerInvariant();

        if (await uow.Items.ExistsBySkuAsync(sku, ct))
            throw new ConflictException($"Item with SKU '{sku}' already exists.");
        if (await uow.Items.ExistsBySlugAsync(slug, ct: ct))
            throw new ConflictException($"Item with slug '{slug}' already exists.");
        if (!await uow.Categories.ExistsAsync(input.CategoryId, ct))
            throw new ValidationException(nameof(CreateItemDto.CategoryId), $"Category {input.CategoryId} does not exist.");

        foreach (var tagId in input.TagIds)
            if (await uow.Tags.GetByIdAsync(tagId, ct) is null)
                throw new ValidationException(nameof(CreateItemDto.TagIds), $"Tag {tagId} does not exist.");

        var item = new Item(input.CategoryId, input.SellerExternalId, sku, input.Title, slug,
            input.EstimateLow, input.EstimateHigh, input.Currency);

        if (input.Detail is { } d)
            item.SetDetail(Enum.Parse<ItemCondition>(d.Condition, ignoreCase: true), d.ProductionYear,
                d.CountryOfOrigin, d.CertificateNumber, d.Provenance, d.Description);

        foreach (var url in input.ImageUrls)
            item.AddImage(url);

        foreach (var tagId in input.TagIds)
            item.AddTag(tagId);

        await uow.Items.AddAsync(item, ct);
        await uow.SaveChangesAsync(ct);

        return await GetByIdAsync(item.Id, ct);
    }

    public async Task UpdateAsync(long id, UpdateItemDto input, CancellationToken ct = default)
    {
        var item = await uow.Items.GetByIdAsync(id, ct)
            ?? throw new EntityNotFoundException(nameof(Item), id);

        var slug = input.Slug.Trim().ToLowerInvariant();
        if (await uow.Items.ExistsBySlugAsync(slug, id, ct))
            throw new ConflictException($"Item with slug '{slug}' already exists.");
        if (input.CategoryId != item.CategoryId && !await uow.Categories.ExistsAsync(input.CategoryId, ct))
            throw new ValidationException(nameof(UpdateItemDto.CategoryId), $"Category {input.CategoryId} does not exist.");

        item.Rename(input.Title, slug);
        item.ChangeEstimate(input.EstimateLow, input.EstimateHigh);
        item.MoveToCategory(input.CategoryId);

        uow.Items.Update(item);
        await uow.SaveChangesAsync(ct);
    }

    public async Task ChangeStatusAsync(long id, ChangeItemStatusDto input, CancellationToken ct = default)
    {
        var item = await uow.Items.GetByIdAsync(id, ct)
            ?? throw new EntityNotFoundException(nameof(Item), id);

        switch (Enum.Parse<ItemStatus>(input.Status, ignoreCase: true))
        {
            case ItemStatus.Listed: item.List(); break;
            case ItemStatus.InAuction: item.SendToAuction(); break;
            case ItemStatus.Sold: item.MarkSold(); break;
            case ItemStatus.Withdrawn: item.Withdraw(); break;
            default: throw new DomainException($"Status '{input.Status}' cannot be set directly.");
        }

        await uow.SaveChangesAsync(ct);
    }

    public async Task AddTagAsync(long id, long tagId, CancellationToken ct = default)
    {
        var item = await uow.Items.GetWithDetailsAsync(id, asTracking: true, ct)
            ?? throw new EntityNotFoundException(nameof(Item), id);
        _ = await uow.Tags.GetByIdAsync(tagId, ct)
            ?? throw new EntityNotFoundException(nameof(Tag), tagId);

        item.AddTag(tagId);
        await uow.SaveChangesAsync(ct);
    }

    public async Task RemoveTagAsync(long id, long tagId, CancellationToken ct = default)
    {
        var item = await uow.Items.GetWithDetailsAsync(id, asTracking: true, ct)
            ?? throw new EntityNotFoundException(nameof(Item), id);

        item.RemoveTag(tagId);
        await uow.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        var item = await uow.Items.GetByIdAsync(id, ct)
            ?? throw new EntityNotFoundException(nameof(Item), id);

        item.EnsureCanBeDeleted();
        uow.Items.Delete(item);
        await uow.SaveChangesAsync(ct);
    }
}
