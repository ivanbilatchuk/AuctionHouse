using Catalog.Bll.DTO;
using Catalog.Domain.Entities;
using FluentValidation;

namespace Catalog.Bll.Validation;

public sealed class CreateCategoryDtoValidator : AbstractValidator<CreateCategoryDto>
{
    public CreateCategoryDtoValidator()
    {
        RuleFor(x => x.Slug).Matches(ValidationRules.SlugPattern)
            .WithMessage("Slug may contain only lower-case letters, digits and single '-' separators.");
        RuleFor(x => x.AttributeKind)
            .Must(kind => Category.AttributeKinds.Contains(kind))
            .WithMessage($"AttributeKind must be one of: {string.Join(", ", Category.AttributeKinds)}.");
        RuleFor(x => x.ParentCategoryId).GreaterThan(0).When(x => x.ParentCategoryId is not null);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}
