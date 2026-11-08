namespace Platform.Aggregator.Dto;

// Відповідь Catalog API GET /api/items/{id}.
public sealed class LotDto
{
    public long Id { get; init; }
    public string Sku { get; init; } = "";
    public string Title { get; init; } = "";
    public string? CategoryName { get; init; }
    public decimal EstimateLow { get; init; }
    public decimal EstimateHigh { get; init; }
    public string Currency { get; init; } = "";
    public string Status { get; init; } = "";
    public LotDetailDto? Detail { get; init; }
    public IReadOnlyList<LotImageDto> Images { get; init; } = [];
}

public sealed class LotDetailDto
{
    public string Condition { get; init; } = "";
    public short? ProductionYear { get; init; }
    public string? CountryOfOrigin { get; init; }
    public string? Provenance { get; init; }
    public string? Description { get; init; }
}

public sealed class LotImageDto
{
    public string Url { get; init; } = "";
    public bool IsPrimary { get; init; }
}
