namespace Community.WebApi.Contracts;

public sealed class CreateExpertOpinionRequest
{
    public long ItemId { get; init; }
    public string ItemTitle { get; init; } = "";
    public string CategoryKind { get; init; } = "";
    public PersonRequest Expert { get; init; } = new();
    public string Verdict { get; init; } = "";
    public int Confidence { get; init; }
    public Dictionary<string, string> Attributes { get; init; } = new();
    public string? Notes { get; init; }
}

public sealed class UpdateExpertOpinionRequest
{
    public string Verdict { get; init; } = "";
    public int Confidence { get; init; }
    public Dictionary<string, string> Attributes { get; init; } = new();
    public string? Notes { get; init; }
    public int Version { get; init; }
}

public sealed class PersonRequest
{
    public Guid UserId { get; init; }
    public string DisplayName { get; init; } = "";
}
