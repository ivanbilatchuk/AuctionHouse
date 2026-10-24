using Microsoft.Data.SqlClient;

namespace Auctions.Dal.Interfaces;

/// <summary>
/// Спільний контекст підключення для всіх репозиторіїв одного Unit of Work:
/// одне з'єднання і (за потреби) одна активна транзакція.
/// </summary>
public interface IDbSession
{
    /// <summary>Таймаут команд у секундах (запобігає нескінченному очікуванню блокувань).</summary>
    int CommandTimeoutSeconds { get; }

    /// <summary>Активна транзакція або null, якщо UoW працює без транзакції.</summary>
    SqlTransaction? Transaction { get; }

    /// <summary>Повертає відкрите з'єднання (відкриває його централізовано при першому зверненні).</summary>
    ValueTask<SqlConnection> GetOpenConnectionAsync(CancellationToken cancellationToken = default);
}
