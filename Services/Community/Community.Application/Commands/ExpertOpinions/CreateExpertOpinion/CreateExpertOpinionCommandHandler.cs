using AutoMapper;
using Community.Application.Caching;
using Community.Application.Diagnostics;
using Community.Application.DTO;
using Community.Domain.Entities;
using Community.Domain.Repositories;
using Community.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Community.Application.Commands.ExpertOpinions.CreateExpertOpinion;

public sealed class CreateExpertOpinionCommandHandler(
    IExpertOpinionRepository repository, IMapper mapper, TwoLevelCache cache, ILogger<CreateExpertOpinionCommandHandler> logger)
    : IRequestHandler<CreateExpertOpinionCommand, ExpertOpinionDto>
{
    public async Task<ExpertOpinionDto> Handle(CreateExpertOpinionCommand request, CancellationToken cancellationToken)
    {
        using var activity = CommunityTelemetry.ActivitySource.StartActivity("CreateExpertOpinion");
        activity?.SetTag("opinion.item_id", request.ItemId);
        activity?.SetTag("opinion.verdict", request.Verdict);

        var opinion = new ExpertOpinion(
            new ItemReference(request.ItemId, request.ItemTitle),
            Enum.Parse<CategoryKind>(request.CategoryKind, ignoreCase: true),
            new Person(request.ExpertUserId, request.ExpertName),
            Enum.Parse<Verdict>(request.Verdict, ignoreCase: true),
            new Confidence(request.Confidence),
            request.Attributes,
            request.Notes);

        await repository.AddAsync(opinion, cancellationToken);
        await cache.InvalidateAsync(CommunityCacheKeys.OpinionsForItemVersion(request.ItemId),
            $"expert opinion {opinion.Id} created", cancellationToken);

        activity?.SetTag("opinion.id", opinion.Id.ToString());
        logger.LogInformation("Expert opinion {OpinionId} created for item {ItemId}: verdict {Verdict}, confidence {Confidence}",
            opinion.Id.ToString(), request.ItemId, opinion.Verdict, request.Confidence);

        return mapper.Map<ExpertOpinionDto>(opinion);
    }
}
