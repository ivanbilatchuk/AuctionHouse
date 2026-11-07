using Community.Application.Abstractions.Messaging;
using Community.Application.DTO;

namespace Community.Application.Queries.Discussions.GetDiscussionById;

public sealed record GetDiscussionByIdQuery(string Id) : IQuery<DiscussionDto>;
