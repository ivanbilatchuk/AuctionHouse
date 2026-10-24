using AutoMapper;
using Catalog.Bll.DTO;
using Catalog.Domain.Entities;

namespace Catalog.Bll.Mapping;

public sealed class CategoryProfile : Profile
{
    public CategoryProfile()
    {
        CreateMap<Category, CategoryDto>();
        CreateMap<Category, CategoryWithItemsDto>();
        CreateMap<Tag, TagDto>();
    }
}
