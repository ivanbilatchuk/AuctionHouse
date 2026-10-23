using Catalog.Domain.Exceptions;

namespace Catalog.Domain.Entities;

public class Item
{
    private readonly List<ItemImage> _images = [];
    private readonly List<ItemTag> _itemTags = [];

    private Item() { }

    public Item(long categoryId, Guid sellerExternalId, string sku, string title, string slug,
        decimal estimateLow, decimal estimateHigh, string currency = "USD")
    {
        if (categoryId <= 0) throw new DomainException("Category is required.");
        if (sellerExternalId == Guid.Empty) throw new DomainException("Seller is required.");
        if (string.IsNullOrWhiteSpace(sku)) throw new DomainException("SKU is required.");
        if (string.IsNullOrWhiteSpace(currency) || currency.Trim().Length != 3)
            throw new DomainException("Currency must be a 3-letter ISO code.");

        CategoryId = categoryId;
        SellerExternalId = sellerExternalId;
        Sku = sku.Trim().ToUpperInvariant();
        Currency = currency.Trim().ToUpperInvariant();
        Status = ItemStatus.Draft;

        Rename(title, slug);
        ChangeEstimate(estimateLow, estimateHigh);
    }

    public long Id { get; private set; }
    public long CategoryId { get; private set; }
    public Guid SellerExternalId { get; private set; }
    public string Sku { get; private set; } = null!;
    public string Title { get; private set; } = null!;
    public string Slug { get; private set; } = null!;
    public decimal EstimateLow { get; private set; }
    public decimal EstimateHigh { get; private set; }
    public string Currency { get; private set; } = "USD";
    public ItemStatus Status { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public string CreatedBy { get; private set; } = null!;
    public DateTime? UpdatedAt { get; private set; }
    public string? UpdatedBy { get; private set; }
    public bool IsDeleted { get; private set; }
    public byte[] RowVersion { get; private set; } = null!;

    public Category? Category { get; private set; }
    public ItemDetail? Detail { get; private set; }
    public IReadOnlyCollection<ItemImage> Images => _images;
    public IReadOnlyCollection<ItemTag> ItemTags => _itemTags;

    public bool IsEditable => Status is ItemStatus.Draft or ItemStatus.Listed;

    public void Rename(string title, string slug)
    {
        EnsureEditable();
        if (string.IsNullOrWhiteSpace(title)) throw new DomainException("Title is required.");
        if (string.IsNullOrWhiteSpace(slug)) throw new DomainException("Slug is required.");

        Title = title.Trim();
        Slug = slug.Trim().ToLowerInvariant();
    }

    public void ChangeEstimate(decimal low, decimal high)
    {
        EnsureEditable();
        if (low <= 0) throw new DomainException("Estimate must be positive.");
        if (high < low) throw new DomainException("Upper estimate cannot be lower than the lower estimate.");

        EstimateLow = low;
        EstimateHigh = high;
    }

    public void MoveToCategory(long categoryId)
    {
        EnsureEditable();
        if (categoryId <= 0) throw new DomainException("Category is required.");

        CategoryId = categoryId;
    }

    public void SetDetail(ItemCondition condition, short? productionYear, string? countryOfOrigin,
        string? certificateNumber, string? provenance, string? description)
    {
        if (Detail is null)
            Detail = new ItemDetail(condition, productionYear, countryOfOrigin, certificateNumber, provenance, description);
        else
            Detail.Update(condition, productionYear, countryOfOrigin, certificateNumber, provenance, description);
    }

    public ItemImage AddImage(string url, bool isPrimary = false)
    {
        if (string.IsNullOrWhiteSpace(url) || !url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            throw new DomainException("Image URL must be an absolute http(s) address.");

        var makePrimary = isPrimary || _images.Count == 0;
        if (makePrimary)
            foreach (var image in _images) image.UnmarkPrimary();

        var added = new ItemImage(url, makePrimary, _images.Count);
        _images.Add(added);
        return added;
    }

    public void AddTag(long tagId)
    {
        if (tagId <= 0) throw new DomainException("Tag is required.");
        if (_itemTags.Any(t => t.TagId == tagId))
            throw new DomainException($"Tag {tagId} is already assigned to the item.");

        _itemTags.Add(new ItemTag(this, tagId));
    }

    public void RemoveTag(long tagId)
    {
        var itemTag = _itemTags.FirstOrDefault(t => t.TagId == tagId)
            ?? throw new DomainException($"Tag {tagId} is not assigned to the item.");

        _itemTags.Remove(itemTag);
    }

    public void List() => TransitionTo(ItemStatus.Listed, ItemStatus.Draft, ItemStatus.Withdrawn);

    public void SendToAuction() => TransitionTo(ItemStatus.InAuction, ItemStatus.Listed);

    public void MarkSold() => TransitionTo(ItemStatus.Sold, ItemStatus.InAuction);

    public void Withdraw() => TransitionTo(ItemStatus.Withdrawn, ItemStatus.Draft, ItemStatus.Listed, ItemStatus.InAuction);

    public void EnsureCanBeDeleted()
    {
        if (Status is ItemStatus.InAuction or ItemStatus.Sold)
            throw new DomainException($"Item in status '{Status}' cannot be deleted.");
    }

    private void TransitionTo(ItemStatus target, params ItemStatus[] allowedFrom)
    {
        if (!allowedFrom.Contains(Status))
            throw new DomainException($"Cannot change status from '{Status}' to '{target}'.");

        Status = target;
    }

    private void EnsureEditable()
    {
        if (Id != 0 && !IsEditable)
            throw new DomainException($"Item in status '{Status}' cannot be edited.");
    }
}
