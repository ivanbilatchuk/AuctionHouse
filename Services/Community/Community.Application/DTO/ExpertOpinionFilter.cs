namespace Community.Application.DTO;

public sealed record ExpertOpinionFilter(
    int Page,
    int PageSize,
    long? ItemId,
    string? CategoryKind,
    string? Verdict,
    int? MinConfidence,
    string? Search,
    string SortBy,
    string SortDir);
