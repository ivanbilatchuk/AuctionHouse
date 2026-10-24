using Community.Application.Abstractions.Messaging;
using Community.Application.DTO;

namespace Community.Application.Queries.ExpertOpinions.GetExpertOpinionById;

public sealed record GetExpertOpinionByIdQuery(string Id) : IQuery<ExpertOpinionDto>;
