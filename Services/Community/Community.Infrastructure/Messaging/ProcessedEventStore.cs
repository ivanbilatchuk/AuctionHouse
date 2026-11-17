using Community.Application.Abstractions.Persistence;
using Community.Infrastructure.Persistence;
using MongoDB.Driver;

namespace Community.Infrastructure.Messaging;

// processedEvents: { _id: EventId (UUID), eventType, processedAt }. Унікальність _id — гарантія від подвійного позначення;
// TTL-індекс (MongoIndexInitializer) прибирає записи, старші за термін можливої повторної доставки.
public sealed class ProcessedEventStore(MongoDbContext context) : IProcessedEventStore
{
    public async Task<bool> IsProcessedAsync(Guid eventId, CancellationToken ct = default)
        => await context.ProcessedEvents.Find(e => e.Id == eventId).Limit(1).AnyAsync(ct);

    public async Task<bool> TryMarkProcessedAsync(Guid eventId, string eventType, CancellationToken ct = default)
    {
        try
        {
            await context.ProcessedEvents.InsertOneAsync(new ProcessedEventDocument(eventId, eventType, DateTime.UtcNow), cancellationToken: ct);
            return true;
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            return false;
        }
    }
}

public sealed record ProcessedEventDocument(Guid Id, string EventType, DateTime ProcessedAt);
