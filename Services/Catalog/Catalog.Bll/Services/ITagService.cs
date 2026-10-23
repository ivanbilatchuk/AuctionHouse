using Catalog.Bll.DTO;

namespace Catalog.Bll.Services;

public interface ITagService
{
    Task<IReadOnlyList<TagDto>> GetAllAsync(CancellationToken ct = default);

    Task<IReadOnlyList<TaggedItemDto>> GetRecentlyTaggedAsync(string slug, DateTime? since, CancellationToken ct = default);
}
