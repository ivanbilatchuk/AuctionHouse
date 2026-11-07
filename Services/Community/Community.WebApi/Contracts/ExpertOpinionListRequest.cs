namespace Community.WebApi.Contracts;

public sealed class ExpertOpinionListRequest
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public long? ItemId { get; init; }
    public string? CategoryKind { get; init; }
    public string? Verdict { get; init; }
    public int? MinConfidence { get; init; }
    public string? Search { get; init; }
    public string SortBy { get; init; } = "createdAt";
    public string SortDir { get; init; } = "desc";
}
