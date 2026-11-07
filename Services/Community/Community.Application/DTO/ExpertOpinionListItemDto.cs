namespace Community.Application.DTO;

public sealed class ExpertOpinionListItemDto
{
    public string Id { get; init; } = "";
    public long ItemId { get; init; }
    public string ItemTitle { get; init; } = "";
    public string CategoryKind { get; init; } = "";
    public string ExpertName { get; init; } = "";
    public string Verdict { get; init; } = "";
    public int Confidence { get; init; }
    public DateTime CreatedAt { get; init; }
}
