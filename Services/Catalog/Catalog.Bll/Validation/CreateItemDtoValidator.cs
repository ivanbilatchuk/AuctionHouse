using Catalog.Bll.DTO;
using Catalog.Domain.Entities;
using FluentValidation;

namespace Catalog.Bll.Validation;

public sealed class CreateItemDtoValidator : AbstractValidator<CreateItemDto>
{
    public CreateItemDtoValidator()
    {
        RuleFor(x => x.SellerExternalId).NotEmpty();
        RuleFor(x => x.Sku).Matches(ValidationRules.SkuPattern)
            .WithMessage("SKU may contain only upper-case letters, digits and '-'.");
        RuleFor(x => x.Slug).Matches(ValidationRules.SlugPattern)
            .WithMessage("Slug may contain only lower-case letters, digits and single '-' separators.");
        RuleFor(x => x.Currency).Matches(ValidationRules.CurrencyPattern)
            .WithMessage("Currency must be an upper-case ISO 4217 code, e.g. USD.");
        RuleFor(x => x.EstimateLow).GreaterThan(0m);
        RuleFor(x => x.EstimateHigh).GreaterThanOrEqualTo(x => x.EstimateLow);

        RuleForEach(x => x.ImageUrls)
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            .WithMessage("Image URL must be an absolute http(s) address.");
        RuleFor(x => x.TagIds).Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("Tag ids must be unique.");
        RuleForEach(x => x.TagIds).GreaterThan(0);

        When(x => x.Detail is not null, () =>
        {
            RuleFor(x => x.Detail!.Condition).IsEnumName(typeof(ItemCondition), caseSensitive: false)
                .WithMessage($"Condition must be one of: {string.Join(", ", Enum.GetNames<ItemCondition>())}.");
            RuleFor(x => x.Detail!.ProductionYear).InclusiveBetween((short)1000, (short)2100)
                .When(x => x.Detail!.ProductionYear is not null);
        });
    }
}
