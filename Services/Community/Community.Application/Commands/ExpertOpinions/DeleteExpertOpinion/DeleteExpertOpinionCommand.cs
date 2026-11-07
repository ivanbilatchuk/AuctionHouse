using Community.Application.Abstractions.Messaging;

namespace Community.Application.Commands.ExpertOpinions.DeleteExpertOpinion;

public sealed record DeleteExpertOpinionCommand(string Id) : ICommand;
