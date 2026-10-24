using Community.Application.Validation;
using FluentValidation;

namespace Community.Application.Queries.ExpertOpinions.GetExpertOpinionById;

public sealed class GetExpertOpinionByIdQueryValidator : AbstractValidator<GetExpertOpinionByIdQuery>
{
    public GetExpertOpinionByIdQueryValidator() => RuleFor(x => x.Id).MustBeObjectId();
}
