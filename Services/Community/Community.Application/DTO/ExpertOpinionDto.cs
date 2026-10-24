namespace Community.Application.DTO;

public sealed class ExpertOpinionDto
{
    public string Id { get; init; } = "";
    public long ItemId { get; init; }
    public string ItemTitle { get; init; } = "";
    public string CategoryKind { get; init; } = "";
    public PersonDto Expert { get; init; } = new();
    public string Verdict { get; init; } = "";
    public int Confidence { get; init; }
    public Dictionary<string, string> Attributes { get; init; } = new();
    public string? Notes { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public int Version { get; init; }
}
