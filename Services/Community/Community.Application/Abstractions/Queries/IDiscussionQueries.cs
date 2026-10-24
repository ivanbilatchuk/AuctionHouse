using Community.Application.DTO;

namespace Community.Application.Abstractions.Queries;

public interface IDiscussionQueries
{
    Task<DiscussionDto?> GetByIdAsync(string id, CancellationToken ct = default);
}
