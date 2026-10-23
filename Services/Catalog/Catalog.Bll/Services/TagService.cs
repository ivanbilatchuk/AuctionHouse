using AutoMapper;
using Catalog.Bll.DTO;
using Catalog.Bll.Specifications;
using Catalog.Dal.Interfaces;
using Catalog.Domain.Entities;
using Catalog.Domain.Exceptions;

namespace Catalog.Bll.Services;

public sealed class TagService(IUnitOfWork uow, IMapper mapper, TimeProvider timeProvider) : ITagService
{
    public async Task<IReadOnlyList<TagDto>> GetAllAsync(CancellationToken ct = default)
    {
        var tags = await uow.Tags.GetAllAsync(ct);
        return mapper.Map<List<TagDto>>(tags.OrderBy(t => t.Name).ToList());
    }

    public async Task<IReadOnlyList<TaggedItemDto>> GetRecentlyTaggedAsync(string slug, DateTime? since, CancellationToken ct = default)
    {
        var normalized = slug.Trim().ToLowerInvariant();
        _ = await uow.Tags.FirstOrDefaultAsync(new TagBySlugSpec(normalized), ct)
            ?? throw new EntityNotFoundException(nameof(Tag), normalized);

        var from = since switch
        {
            null => timeProvider.GetUtcNow().UtcDateTime.AddDays(-30),
            { Kind: DateTimeKind.Unspecified } value => DateTime.SpecifyKind(value, DateTimeKind.Utc),
            { } value => value.ToUniversalTime()
        };
        var itemTags = await uow.ItemTags.GetRecentlyTaggedAsync(normalized, from, ct);

        return mapper.Map<List<TaggedItemDto>>(itemTags);
    }
}
