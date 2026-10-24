using AutoMapper;
using Community.Application.DTO;
using Community.Domain.Entities;
using Community.Domain.ValueObjects;
using MongoDB.Bson;

namespace Community.Application.Mapping;

// ObjectId -> string, Value Objects -> DTO / примітиви, вкладені масиви коментарів -> CommentDto.
public sealed class ApplicationProfile : Profile
{
    public ApplicationProfile()
    {
        CreateMap<ObjectId, string>().ConvertUsing(id => id.ToString());
        CreateMap<Confidence, int>().ConvertUsing(c => c.Value);
        CreateMap<Person, PersonDto>();

        CreateMap<ExpertOpinion, ExpertOpinionDto>()
            .ForMember(d => d.ItemId, o => o.MapFrom(s => s.Item.ItemId))
            .ForMember(d => d.ItemTitle, o => o.MapFrom(s => s.Item.Title))
            .ForMember(d => d.Attributes, o => o.MapFrom(s => s.Attributes.ToDictionary(a => a.Key, a => a.Value)));

        CreateMap<Discussion, DiscussionDto>()
            .ForMember(d => d.ItemId, o => o.MapFrom(s => s.Item.ItemId))
            .ForMember(d => d.ItemTitle, o => o.MapFrom(s => s.Item.Title));

        CreateMap<Comment, CommentDto>();
    }
}
