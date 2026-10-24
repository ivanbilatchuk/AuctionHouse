using Auctions.Domain.Exceptions;
using Microsoft.Data.SqlClient;

namespace Auctions.Dal.Repositories;

internal static class SqlExceptionTranslator
{
    public static Exception Translate(SqlException ex) => ex.Number switch
    {
        50000 or 50042 => new NotFoundException(ex.Message),
        50011 => new ValidationException(ex.Message),
        2601 or 2627 => new BusinessConflictException("A record with the same unique value already exists.", ex),
        547 => new BusinessConflictException("The operation violates a data integrity constraint.", ex),
        1205 => new BusinessConflictException("The operation was chosen as a deadlock victim. Please retry.", ex),
        >= 50000 and <= 59999 => new BusinessConflictException(ex.Message, ex),
        _ => new DataAccessException("A database error occurred.", ex)
    };
}
