using Community.Domain.Entities;
using Community.Domain.Exceptions;
using Community.Domain.Repositories;
using MediatR;
using MongoDB.Bson;

namespace Community.Application.Commands.ExpertOpinions.DeleteExpertOpinion;

public sealed class DeleteExpertOpinionCommandHandler(IExpertOpinionRepository repository)
    : IRequestHandler<DeleteExpertOpinionCommand>
{
    public async Task Handle(DeleteExpertOpinionCommand request, CancellationToken cancellationToken)
    {
        if (!await repository.DeleteAsync(ObjectId.Parse(request.Id), cancellationToken))
            throw new NotFoundException(nameof(ExpertOpinion), request.Id);
    }
}
