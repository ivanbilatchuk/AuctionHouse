namespace Platform.Aggregator.Dto;

// Елемент сторінки Community API GET /api/expert-opinions?itemId=.
public sealed class ExpertOpinionDto
{
    public string Id { get; init; } = "";
    public long ItemId { get; init; }
    public string CategoryKind { get; init; } = "";
    public string ExpertName { get; init; } = "";
    public string Verdict { get; init; } = "";
    public int Confidence { get; init; }
    public DateTime CreatedAt { get; init; }
}

// Обгортка пагінації Community API (items + totalCount).
public sealed class ExpertOpinionPageDto
{
    public IReadOnlyList<ExpertOpinionDto> Items { get; init; } = [];
    public long TotalCount { get; init; }
}
