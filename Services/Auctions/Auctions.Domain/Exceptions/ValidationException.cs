namespace Auctions.Domain.Exceptions;

public class ValidationException : Exception
{
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public ValidationException(string message)
        : this(message, new Dictionary<string, string[]>()) { }

    public ValidationException(string field, string message)
        : this(message, new Dictionary<string, string[]> { [field] = new[] { message } }) { }

    public ValidationException(string message, IReadOnlyDictionary<string, string[]> errors) : base(message)
    {
        Errors = errors;
    }
}
