using Community.Domain.Common;
using Community.Domain.Exceptions;

namespace Community.Domain.ValueObjects;

// Упевненість експерта у висновку, 0..100 %.
public sealed class Confidence : ValueObject
{
    public Confidence(int value)
    {
        if (value is < 0 or > 100) throw new DomainException("Confidence must be between 0 and 100.");
        Value = value;
    }

    public int Value { get; }

    public bool IsHigh => Value >= 80;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
