using AutoMapper;
using Community.Application.DTO;
using Community.Domain.Entities;
using Community.Domain.Repositories;
using Community.Domain.ValueObjects;
using MediatR;

namespace Community.Application.Commands.ExpertOpinions.CreateExpertOpinion;

public sealed class CreateExpertOpinionCommandHandler(IExpertOpinionRepository repository, IMapper mapper)
    : IRequestHandler<CreateExpertOpinionCommand, ExpertOpinionDto>
{
    public async Task<ExpertOpinionDto> Handle(CreateExpertOpinionCommand request, CancellationToken cancellationToken)
    {
        var opinion = new ExpertOpinion(
            new ItemReference(request.ItemId, request.ItemTitle),
            Enum.Parse<CategoryKind>(request.CategoryKind, ignoreCase: true),
            new Person(request.ExpertUserId, request.ExpertName),
            Enum.Parse<Verdict>(request.Verdict, ignoreCase: true),
            new Confidence(request.Confidence),
            request.Attributes,
            request.Notes);

        await repository.AddAsync(opinion, cancellationToken);

        return mapper.Map<ExpertOpinionDto>(opinion);
    }
}
