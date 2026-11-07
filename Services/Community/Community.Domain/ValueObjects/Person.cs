using Community.Domain.Common;
using Community.Domain.Exceptions;

namespace Community.Domain.ValueObjects;

// Знімок користувача з Identity-контексту (експерт або автор коментаря).
public sealed class Person : ValueObject
{
    public Person(Guid userId, string displayName)
    {
        if (userId == Guid.Empty) throw new DomainException("User id is required.");
        if (string.IsNullOrWhiteSpace(displayName)) throw new DomainException("Display name is required.");
        if (displayName.Length > 100) throw new DomainException("Display name must be at most 100 characters.");

        UserId = userId;
        DisplayName = displayName.Trim();
    }

    public Guid UserId { get; }

    public string DisplayName { get; }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return UserId;
        yield return DisplayName;
    }
}
