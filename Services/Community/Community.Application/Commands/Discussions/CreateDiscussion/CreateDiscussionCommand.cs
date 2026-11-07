using Community.Application.Abstractions.Messaging;
using Community.Application.DTO;

namespace Community.Application.Commands.Discussions.CreateDiscussion;

public sealed record CreateDiscussionCommand : ICommand<DiscussionDto>
{
    public long AuctionId { get; init; }
    public long ItemId { get; init; }
    public string ItemTitle { get; init; } = "";
}
