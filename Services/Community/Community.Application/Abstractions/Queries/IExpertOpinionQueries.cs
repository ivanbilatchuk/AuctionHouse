using Community.Application.Common;
using Community.Application.DTO;

namespace Community.Application.Abstractions.Queries;

// Інтерфейс читання (read side CQRS); реалізація в Infrastructure проєктує документи одразу в DTO.
public interface IExpertOpinionQueries
{
    Task<ExpertOpinionDto?> GetByIdAsync(string id, CancellationToken ct = default);

    Task<PagedResult<ExpertOpinionListItemDto>> GetListAsync(ExpertOpinionFilter filter, CancellationToken ct = default);
}
