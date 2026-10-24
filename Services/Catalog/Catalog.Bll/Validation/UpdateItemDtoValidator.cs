using Catalog.Bll.DTO;
using FluentValidation;

namespace Catalog.Bll.Validation;

public sealed class UpdateItemDtoValidator : AbstractValidator<UpdateItemDto>
{
    public UpdateItemDtoValidator()
    {
        RuleFor(x => x.Slug).Matches(ValidationRules.SlugPattern)
            .WithMessage("Slug may contain only lower-case letters, digits and single '-' separators.");
        RuleFor(x => x.EstimateLow).GreaterThan(0m);
        RuleFor(x => x.EstimateHigh).GreaterThanOrEqualTo(x => x.EstimateLow);
    }
}
