using Community.Application.Abstractions.Messaging;
using Community.Application.Common;
using Community.Application.DTO;

namespace Community.Application.Queries.ExpertOpinions.GetExpertOpinions;

public sealed record GetExpertOpinionsQuery : IQuery<PagedResult<ExpertOpinionListItemDto>>
{
    public const int MaxPageSize = 100;
    public static readonly string[] SortFields = ["createdAt", "confidence"];

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
