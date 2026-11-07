using Community.Application.Abstractions.Messaging;
using Community.Application.DTO;

namespace Community.Application.Commands.ExpertOpinions.CreateExpertOpinion;

public sealed record CreateExpertOpinionCommand : ICommand<ExpertOpinionDto>
{
    public long ItemId { get; init; }
    public string ItemTitle { get; init; } = "";
    public string CategoryKind { get; init; } = "";
    public Guid ExpertUserId { get; init; }
    public string ExpertName { get; init; } = "";
    public string Verdict { get; init; } = "";
    public int Confidence { get; init; }
    public Dictionary<string, string> Attributes { get; init; } = new();
    public string? Notes { get; init; }
}
