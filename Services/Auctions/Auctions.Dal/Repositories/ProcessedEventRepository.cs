using Auctions.Dal.Interfaces;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Auctions.Dal.Repositories;

public sealed class ProcessedEventRepository(IDbSession session) : IProcessedEventRepository
{
    // INSERT ... WHERE NOT EXISTS: звичайний дублікат не породжує винятку; конкурентний дублікат (два екземпляри
    // сервісу отримали ту саму подію) зупиняє PRIMARY KEY — помилка 2627 теж означає «уже оброблено».
    private const string InsertSql = """
        INSERT dbo.ProcessedEvents (EventId, EventType)
        SELECT @EventId, @EventType
        WHERE NOT EXISTS (SELECT 1 FROM dbo.ProcessedEvents WITH (UPDLOCK, HOLDLOCK) WHERE EventId = @EventId);
        """;

    public async Task<bool> TryAddAsync(Guid eventId, string eventType, CancellationToken cancellationToken = default)
    {
        try
        {
            var connection = await session.GetOpenConnectionAsync(cancellationToken);
            var inserted = await connection.ExecuteAsync(new CommandDefinition(
                InsertSql, new { EventId = eventId, EventType = eventType }, session.Transaction,
                session.CommandTimeoutSeconds, cancellationToken: cancellationToken));
            return inserted == 1;
        }
        catch (SqlException ex) when (ex.Number is 2627 or 2601)
        {
            return false;
        }
        catch (SqlException ex)
        {
            throw SqlExceptionTranslator.Translate(ex);
        }
    }
}
