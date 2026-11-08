using Community.Application.Abstractions.Messaging;
using Community.Application.Caching;
using Community.Application.Common;
using Community.Application.DTO;

namespace Community.Application.Queries.ExpertOpinions.GetExpertOpinions;

public sealed record GetExpertOpinionsQuery : IQuery<PagedResult<ExpertOpinionListItemDto>>, ICacheableQuery
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

    // Кешуються лише сторінки "висновки для лота" з типовим сортуванням (так їх читає Aggregator);
    // запити з фільтрами й пошуком завжди йдуть у MongoDB.
    public CacheDescriptor? GetCacheDescriptor()
    {
        if (ItemId is not { } itemId
            || CategoryKind is not null || Verdict is not null || MinConfidence is not null || Search is not null
            || !string.Equals(SortBy, "createdAt", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(SortDir, "desc", StringComparison.OrdinalIgnoreCase))
            return null;

        return new CacheDescriptor(
            $"{CommunityCacheKeys.OpinionsForItem(itemId)}:p:{Page}:s:{PageSize}",
            CommunityCacheKeys.OpinionsForItemVersion(itemId),
            CommunityCacheKeys.OpinionsSettings);
    }
}
