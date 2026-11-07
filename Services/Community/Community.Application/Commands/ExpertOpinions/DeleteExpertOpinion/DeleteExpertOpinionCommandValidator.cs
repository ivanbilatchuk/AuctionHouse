using Community.Application.Validation;
using FluentValidation;

namespace Community.Application.Commands.ExpertOpinions.DeleteExpertOpinion;

public sealed class DeleteExpertOpinionCommandValidator : AbstractValidator<DeleteExpertOpinionCommand>
{
    public DeleteExpertOpinionCommandValidator() => RuleFor(x => x.Id).MustBeObjectId();
}
