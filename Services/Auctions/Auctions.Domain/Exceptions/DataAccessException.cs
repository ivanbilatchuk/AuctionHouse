namespace Auctions.Domain.Exceptions;

public class DataAccessException : Exception
{
    public DataAccessException(string message, Exception innerException) : base(message, innerException) { }
}
