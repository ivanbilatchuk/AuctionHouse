using Community.Application.Abstractions.Queries;
using Community.Application.DTO;
using Community.Domain.Entities;
using Community.Domain.Exceptions;
using MediatR;

namespace Community.Application.Queries.ExpertOpinions.GetExpertOpinionById;

public sealed class GetExpertOpinionByIdQueryHandler(IExpertOpinionQueries queries)
    : IRequestHandler<GetExpertOpinionByIdQuery, ExpertOpinionDto>
{
    public async Task<ExpertOpinionDto> Handle(GetExpertOpinionByIdQuery request, CancellationToken cancellationToken)
        => await queries.GetByIdAsync(request.Id, cancellationToken)
           ?? throw new NotFoundException(nameof(ExpertOpinion), request.Id);
}
