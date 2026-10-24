using Community.Application.Abstractions.Queries;
using Community.Application.DTO;
using Community.Domain.Entities;
using Community.Domain.Exceptions;
using MediatR;

namespace Community.Application.Queries.Discussions.GetDiscussionById;

public sealed class GetDiscussionByIdQueryHandler(IDiscussionQueries queries)
    : IRequestHandler<GetDiscussionByIdQuery, DiscussionDto>
{
    public async Task<DiscussionDto> Handle(GetDiscussionByIdQuery request, CancellationToken cancellationToken)
        => await queries.GetByIdAsync(request.Id, cancellationToken)
           ?? throw new NotFoundException(nameof(Discussion), request.Id);
}
