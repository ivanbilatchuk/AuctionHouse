using AutoMapper;
using Community.Application.Commands.Discussions.AddComment;
using Community.Application.Commands.Discussions.CreateDiscussion;
using Community.Application.Commands.ExpertOpinions.CreateExpertOpinion;
using Community.Application.Commands.ExpertOpinions.UpdateExpertOpinion;
using Community.Application.Queries.ExpertOpinions.GetExpertOpinions;
using Community.WebApi.Contracts;

namespace Community.WebApi.Mapping;

// HTTP-контракти -> команди й запити MediatR; вкладений PersonRequest розгортається в поля команди.
public sealed class RequestProfile : Profile
{
    public RequestProfile()
    {
        CreateMap<CreateExpertOpinionRequest, CreateExpertOpinionCommand>()
            .ForMember(c => c.ExpertUserId, o => o.MapFrom(r => r.Expert.UserId))
            .ForMember(c => c.ExpertName, o => o.MapFrom(r => r.Expert.DisplayName));

        CreateMap<UpdateExpertOpinionRequest, UpdateExpertOpinionCommand>()
            .ForMember(c => c.Id, o => o.Ignore());

        CreateMap<ExpertOpinionListRequest, GetExpertOpinionsQuery>();

        CreateMap<CreateDiscussionRequest, CreateDiscussionCommand>();

        CreateMap<AddCommentRequest, AddCommentCommand>()
            .ForMember(c => c.DiscussionId, o => o.Ignore())
            .ForMember(c => c.AuthorUserId, o => o.MapFrom(r => r.Author.UserId))
            .ForMember(c => c.AuthorName, o => o.MapFrom(r => r.Author.DisplayName));
    }
}
