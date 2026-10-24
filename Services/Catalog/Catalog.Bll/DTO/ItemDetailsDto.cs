namespace Catalog.Bll.DTO;

public sealed class ItemDetailsDto
{
    public long Id { get; init; }
    public string Sku { get; init; } = "";
    public string Title { get; init; } = "";
    public string Slug { get; init; } = "";
    public Guid SellerExternalId { get; init; }
    public long CategoryId { get; init; }
    public string? CategoryName { get; init; }
    public decimal EstimateLow { get; init; }
    public decimal EstimateHigh { get; init; }
    public string Currency { get; init; } = "";
    public string Status { get; init; } = "";
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public ItemDetailDto? Detail { get; init; }
    public IReadOnlyList<ItemImageDto> Images { get; init; } = [];
    public IReadOnlyList<ItemTagDto> Tags { get; init; } = [];
}

public sealed class ItemDetailDto
{
    public string Condition { get; init; } = "";
    public short? ProductionYear { get; init; }
    public string? CountryOfOrigin { get; init; }
    public string? CertificateNumber { get; init; }
    public string? Provenance { get; init; }
    public string? Description { get; init; }
}

public sealed class ItemImageDto
{
    public long Id { get; init; }
    public string Url { get; init; } = "";
    public bool IsPrimary { get; init; }
    public int SortOrder { get; init; }
}

public sealed class ItemTagDto
{
    public long TagId { get; init; }
    public string Name { get; init; } = "";
    public string Slug { get; init; } = "";
    public DateTime AddedAt { get; init; }
}
