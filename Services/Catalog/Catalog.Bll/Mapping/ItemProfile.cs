using AutoMapper;
using Catalog.Bll.DTO;
using Catalog.Domain.Entities;

namespace Catalog.Bll.Mapping;

public sealed class ItemProfile : Profile
{
    public ItemProfile()
    {
        CreateMap<Item, ItemDto>()
            .ForMember(d => d.CategoryName, o => o.MapFrom(s => s.Category != null ? s.Category.Name : null));

        CreateMap<Item, ItemDetailsDto>()
            .ForMember(d => d.CategoryName, o => o.MapFrom(s => s.Category != null ? s.Category.Name : null))
            .ForMember(d => d.Images, o => o.MapFrom(s => s.Images.OrderBy(i => i.SortOrder)))
            .ForMember(d => d.Tags, o => o.MapFrom(s => s.ItemTags.OrderBy(t => t.AddedAt)));

        CreateMap<ItemDetail, ItemDetailDto>();
        CreateMap<ItemImage, ItemImageDto>();

        CreateMap<ItemTag, ItemTagDto>()
            .ForMember(d => d.Name, o => o.MapFrom(s => s.Tag.Name))
            .ForMember(d => d.Slug, o => o.MapFrom(s => s.Tag.Slug));

        CreateMap<ItemTag, TaggedItemDto>()
            .ForMember(d => d.Sku, o => o.MapFrom(s => s.Item.Sku))
            .ForMember(d => d.Title, o => o.MapFrom(s => s.Item.Title))
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Item.Status.ToString()))
            .ForMember(d => d.TagSlug, o => o.MapFrom(s => s.Tag.Slug));
    }
}
