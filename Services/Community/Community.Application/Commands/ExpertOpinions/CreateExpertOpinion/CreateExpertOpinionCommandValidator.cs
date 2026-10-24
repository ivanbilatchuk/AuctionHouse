using Community.Application.Validation;
using Community.Domain.Entities;
using Community.Domain.Repositories;
using FluentValidation;

namespace Community.Application.Commands.ExpertOpinions.CreateExpertOpinion;

public sealed class CreateExpertOpinionCommandValidator : AbstractValidator<CreateExpertOpinionCommand>
{
    public CreateExpertOpinionCommandValidator(IExpertOpinionRepository repository)
    {
        RuleFor(x => x.ItemId).GreaterThan(0);
        RuleFor(x => x.ItemTitle).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CategoryKind).MustBeEnum<CreateExpertOpinionCommand, CategoryKind>();
        RuleFor(x => x.ExpertUserId).NotEmpty();
        RuleFor(x => x.ExpertName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Verdict).MustBeEnum<CreateExpertOpinionCommand, Verdict>();
        RuleFor(x => x.Confidence).InclusiveBetween(0, 100);
        RuleFor(x => x.Notes).MaximumLength(ExpertOpinion.MaxNotesLength);
        RuleFor(x => x.Notes).NotEmpty()
            .When(x => !string.Equals(x.Verdict, nameof(Verdict.Authentic), StringComparison.OrdinalIgnoreCase))
            .WithMessage("Notes are required when the verdict is not Authentic.");
        RuleFor(x => x.Attributes)
            .Must((x, attributes) => ValidationRules.HasRequiredAttributes(x.CategoryKind, attributes))
            .WithMessage(x => ValidationRules.RequiredAttributesMessage(x.CategoryKind));

        // Асинхронне правило з запитом до MongoDB: один експерт - один висновок на лот.
        RuleFor(x => x)
            .MustAsync(async (x, ct) => !await repository.ExistsForExpertAsync(x.ItemId, x.ExpertUserId, ct))
            .OverridePropertyName(nameof(CreateExpertOpinionCommand.ExpertUserId))
            .WithMessage("This expert has already published an opinion on the item.")
            .When(x => x.ItemId > 0 && x.ExpertUserId != Guid.Empty);
    }
}
