using Community.Application.Validation;
using FluentValidation;

namespace Community.Application.Queries.Discussions.GetDiscussionById;

public sealed class GetDiscussionByIdQueryValidator : AbstractValidator<GetDiscussionByIdQuery>
{
    public GetDiscussionByIdQueryValidator() => RuleFor(x => x.Id).MustBeObjectId();
}
