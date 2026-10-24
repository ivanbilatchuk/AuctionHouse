using AutoMapper;
using Community.Application.DTO;
using Community.Domain.Entities;
using Community.Domain.Repositories;
using Community.Domain.ValueObjects;
using MediatR;

namespace Community.Application.Commands.Discussions.CreateDiscussion;

public sealed class CreateDiscussionCommandHandler(IDiscussionRepository repository, IMapper mapper)
    : IRequestHandler<CreateDiscussionCommand, DiscussionDto>
{
    public async Task<DiscussionDto> Handle(CreateDiscussionCommand request, CancellationToken cancellationToken)
    {
        var discussion = new Discussion(request.AuctionId, new ItemReference(request.ItemId, request.ItemTitle));

        await repository.AddAsync(discussion, cancellationToken);

        return mapper.Map<DiscussionDto>(discussion);
    }
}
