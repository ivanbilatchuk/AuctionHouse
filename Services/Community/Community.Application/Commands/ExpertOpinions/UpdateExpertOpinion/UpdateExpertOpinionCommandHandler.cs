using Community.Domain.Entities;
using Community.Domain.Exceptions;
using Community.Domain.Repositories;
using Community.Domain.ValueObjects;
using MediatR;
using MongoDB.Bson;

namespace Community.Application.Commands.ExpertOpinions.UpdateExpertOpinion;

public sealed class UpdateExpertOpinionCommandHandler(IExpertOpinionRepository repository)
    : IRequestHandler<UpdateExpertOpinionCommand>
{
    public async Task Handle(UpdateExpertOpinionCommand request, CancellationToken cancellationToken)
    {
        var id = ObjectId.Parse(request.Id);
        var opinion = await repository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(nameof(ExpertOpinion), request.Id);

        if (opinion.Version != request.Version)
            throw new ConflictException($"Expert opinion was modified (current version {opinion.Version}, expected {request.Version}).");

        opinion.Revise(
            Enum.Parse<Verdict>(request.Verdict, ignoreCase: true),
            new Confidence(request.Confidence),
            request.Attributes,
            request.Notes);

        await repository.UpdateAsync(opinion, request.Version, cancellationToken);
    }
}
