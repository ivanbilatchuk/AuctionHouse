namespace Catalog.Bll.DTO;

public sealed class ItemDto
{
    public long Id { get; init; }
    public string Sku { get; init; } = "";
    public string Title { get; init; } = "";
    public string Slug { get; init; } = "";
    public long CategoryId { get; init; }
    public string? CategoryName { get; init; }
    public decimal EstimateLow { get; init; }
    public decimal EstimateHigh { get; init; }
    public string Currency { get; init; } = "";
    public string Status { get; init; } = "";
    public DateTime CreatedAt { get; init; }
}
