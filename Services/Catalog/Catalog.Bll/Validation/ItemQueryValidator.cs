using Catalog.Bll.DTO;
using Catalog.Domain.Entities;
using FluentValidation;

namespace Catalog.Bll.Validation;

public sealed class ItemQueryValidator : AbstractValidator<ItemQuery>
{
    public ItemQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, ItemQuery.MaxPageSize);
        RuleFor(x => x.SortBy)
            .Must(v => ItemQuery.SortFields.Contains(v, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"SortBy must be one of: {string.Join(", ", ItemQuery.SortFields)}.");
        RuleFor(x => x.SortDir)
            .Must(v => ItemQuery.SortDirections.Contains(v, StringComparer.OrdinalIgnoreCase))
            .WithMessage("SortDir must be 'asc' or 'desc'.");
        RuleFor(x => x.Status)
            .IsEnumName(typeof(ItemStatus), caseSensitive: false)
            .WithMessage($"Status must be one of: {string.Join(", ", Enum.GetNames<ItemStatus>())}.")
            .When(x => !string.IsNullOrWhiteSpace(x.Status));
        RuleFor(x => x.MinEstimate).GreaterThanOrEqualTo(0m).When(x => x.MinEstimate is not null);
        RuleFor(x => x.MaxEstimate)
            .GreaterThanOrEqualTo(x => x.MinEstimate!.Value)
            .When(x => x.MinEstimate is not null && x.MaxEstimate is not null);
        RuleFor(x => x.Search).MaximumLength(100);
    }
}
