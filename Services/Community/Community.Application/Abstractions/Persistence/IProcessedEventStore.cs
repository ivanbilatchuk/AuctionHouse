namespace Community.Application.Abstractions.Persistence;

// Колекція processedEvents (Лаба 8): ідемпотентність споживача integration events.
public interface IProcessedEventStore
{
    Task<bool> IsProcessedAsync(Guid eventId, CancellationToken ct = default);

    // false — EventId уже позначено (конкурентна повторна доставка).
    Task<bool> TryMarkProcessedAsync(Guid eventId, string eventType, CancellationToken ct = default);
}
