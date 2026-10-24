namespace Catalog.Bll.DTO;

public sealed class ItemQuery
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public static readonly IReadOnlyList<string> SortFields = ["title", "sku", "estimate", "createdAt"];
    public static readonly IReadOnlyList<string> SortDirections = ["asc", "desc"];

    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = DefaultPageSize;
    public string? Search { get; init; }
    public long? CategoryId { get; init; }
    public string? Status { get; init; }
    public decimal? MinEstimate { get; init; }
    public decimal? MaxEstimate { get; init; }
    public string SortBy { get; init; } = "title";
    public string SortDir { get; init; } = "asc";
}
