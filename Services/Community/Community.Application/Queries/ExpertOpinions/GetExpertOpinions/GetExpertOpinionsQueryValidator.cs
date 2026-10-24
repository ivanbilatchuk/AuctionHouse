using Community.Domain.Entities;
using FluentValidation;

namespace Community.Application.Queries.ExpertOpinions.GetExpertOpinions;

public sealed class GetExpertOpinionsQueryValidator : AbstractValidator<GetExpertOpinionsQuery>
{
    public GetExpertOpinionsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, GetExpertOpinionsQuery.MaxPageSize);
        RuleFor(x => x.ItemId).GreaterThan(0).When(x => x.ItemId is not null);
        RuleFor(x => x.CategoryKind!).IsEnumName(typeof(CategoryKind), caseSensitive: false)
            .When(x => !string.IsNullOrWhiteSpace(x.CategoryKind));
        RuleFor(x => x.Verdict!).IsEnumName(typeof(Verdict), caseSensitive: false)
            .When(x => !string.IsNullOrWhiteSpace(x.Verdict));
        RuleFor(x => x.MinConfidence).InclusiveBetween(0, 100).When(x => x.MinConfidence is not null);
        RuleFor(x => x.Search).MaximumLength(100);
        RuleFor(x => x.SortBy)
            .Must(v => GetExpertOpinionsQuery.SortFields.Contains(v, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"SortBy must be one of: {string.Join(", ", GetExpertOpinionsQuery.SortFields)}.");
        RuleFor(x => x.SortDir)
            .Must(v => v is "asc" or "desc")
            .WithMessage("SortDir must be 'asc' or 'desc'.");
    }
}
