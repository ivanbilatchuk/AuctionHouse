using Catalog.Bll.DTO;
using Catalog.Domain.Entities;
using FluentValidation;

namespace Catalog.Bll.Validation;

public sealed class ChangeItemStatusDtoValidator : AbstractValidator<ChangeItemStatusDto>
{
    public ChangeItemStatusDtoValidator()
    {
        RuleFor(x => x.Status)
            .IsEnumName(typeof(ItemStatus), caseSensitive: false)
            .WithMessage("Status must be one of: Listed, InAuction, Sold, Withdrawn.")
            .NotEqual(nameof(ItemStatus.Draft), StringComparer.OrdinalIgnoreCase)
            .WithMessage("Status must be one of: Listed, InAuction, Sold, Withdrawn.");
    }
}
