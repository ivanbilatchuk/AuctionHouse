using Community.Domain.Entities;
using FluentValidation;
using MongoDB.Bson;

namespace Community.Application.Validation;

public static class ValidationRules
{
    public static IRuleBuilderOptions<T, string> MustBeObjectId<T>(this IRuleBuilder<T, string> rule)
        => rule.NotEmpty().Must(id => ObjectId.TryParse(id, out _))
            .WithMessage("'{PropertyName}' must be a valid 24-character hexadecimal ObjectId.");

    public static IRuleBuilderOptions<T, string?> MustBeObjectIdIfPresent<T>(this IRuleBuilder<T, string?> rule)
        => rule.Must(id => id is null || ObjectId.TryParse(id, out _))
            .WithMessage("'{PropertyName}' must be a valid 24-character hexadecimal ObjectId.");

    public static IRuleBuilderOptions<T, string> MustBeEnum<T, TEnum>(this IRuleBuilder<T, string> rule) where TEnum : struct, Enum
        => rule.NotEmpty().IsEnumName(typeof(TEnum), caseSensitive: false)
            .WithMessage($"'{{PropertyName}}' must be one of: {string.Join(", ", Enum.GetNames<TEnum>())}.");

    public static bool HasRequiredAttributes(string categoryKind, IReadOnlyDictionary<string, string> attributes)
        => !Enum.TryParse<CategoryKind>(categoryKind, ignoreCase: true, out var kind)
           || ExpertOpinion.RequiredAttributes[kind].All(attributes.ContainsKey);

    public static string RequiredAttributesMessage(string categoryKind)
        => Enum.TryParse<CategoryKind>(categoryKind, ignoreCase: true, out var kind)
            ? $"Attributes for {kind} must contain: {string.Join(", ", ExpertOpinion.RequiredAttributes[kind])}."
            : "Attributes are invalid.";
}
