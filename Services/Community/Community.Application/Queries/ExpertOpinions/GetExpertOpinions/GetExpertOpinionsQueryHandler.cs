using Community.Application.Abstractions.Queries;
using Community.Application.Common;
using Community.Application.DTO;
using MediatR;

namespace Community.Application.Queries.ExpertOpinions.GetExpertOpinions;

public sealed class GetExpertOpinionsQueryHandler(IExpertOpinionQueries queries)
    : IRequestHandler<GetExpertOpinionsQuery, PagedResult<ExpertOpinionListItemDto>>
{
    public Task<PagedResult<ExpertOpinionListItemDto>> Handle(GetExpertOpinionsQuery request, CancellationToken cancellationToken)
        => queries.GetListAsync(new ExpertOpinionFilter(
            request.Page, request.PageSize, request.ItemId, request.CategoryKind, request.Verdict,
            request.MinConfidence, request.Search, request.SortBy, request.SortDir), cancellationToken);
}
