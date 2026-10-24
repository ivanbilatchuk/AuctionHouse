using Community.Application.Validation;
using Community.Domain.Entities;
using FluentValidation;

namespace Community.Application.Commands.ExpertOpinions.UpdateExpertOpinion;

public sealed class UpdateExpertOpinionCommandValidator : AbstractValidator<UpdateExpertOpinionCommand>
{
    public UpdateExpertOpinionCommandValidator()
    {
        RuleFor(x => x.Id).MustBeObjectId();
        RuleFor(x => x.Verdict).MustBeEnum<UpdateExpertOpinionCommand, Verdict>();
        RuleFor(x => x.Confidence).InclusiveBetween(0, 100);
        RuleFor(x => x.Notes).MaximumLength(ExpertOpinion.MaxNotesLength);
        RuleFor(x => x.Notes).NotEmpty()
            .When(x => !string.Equals(x.Verdict, nameof(Verdict.Authentic), StringComparison.OrdinalIgnoreCase))
            .WithMessage("Notes are required when the verdict is not Authentic.");
        RuleFor(x => x.Version).GreaterThanOrEqualTo(1);
    }
}
