using Community.Application.Abstractions.Messaging;

namespace Community.Application.Commands.ExpertOpinions.UpdateExpertOpinion;

public sealed record UpdateExpertOpinionCommand : ICommand
{
    public string Id { get; init; } = "";
    public string Verdict { get; init; } = "";
    public int Confidence { get; init; }
    public Dictionary<string, string> Attributes { get; init; } = new();
    public string? Notes { get; init; }

    // Версія, яку бачив клієнт; якщо документ уже змінено іншим запитом - 409.
    public int Version { get; init; }
}
