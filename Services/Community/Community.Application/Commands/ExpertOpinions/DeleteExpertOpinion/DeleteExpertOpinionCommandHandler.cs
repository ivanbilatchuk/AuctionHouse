using Community.Application.Caching;
using Community.Domain.Entities;
using Community.Domain.Exceptions;
using Community.Domain.Repositories;
using MediatR;
using MongoDB.Bson;

namespace Community.Application.Commands.ExpertOpinions.DeleteExpertOpinion;

public sealed class DeleteExpertOpinionCommandHandler(IExpertOpinionRepository repository, TwoLevelCache cache)
    : IRequestHandler<DeleteExpertOpinionCommand>
{
    public async Task Handle(DeleteExpertOpinionCommand request, CancellationToken cancellationToken)
    {
        var id = ObjectId.Parse(request.Id);
        // Читаємо висновок до видалення, щоб знати, кеш якого лота інвалідувати.
        var opinion = await repository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(nameof(ExpertOpinion), request.Id);

        if (!await repository.DeleteAsync(id, cancellationToken))
            throw new NotFoundException(nameof(ExpertOpinion), request.Id);

        await cache.InvalidateAsync(CommunityCacheKeys.OpinionsForItemVersion(opinion.Item.ItemId),
            $"expert opinion {request.Id} deleted", cancellationToken);
    }
}
