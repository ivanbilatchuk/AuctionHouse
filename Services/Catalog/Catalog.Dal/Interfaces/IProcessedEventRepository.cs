namespace Catalog.Dal.Interfaces;

// Таблиця оброблених integration events (ідемпотентність споживача).
public interface IProcessedEventRepository
{
    Task<bool> ExistsAsync(Guid eventId, CancellationToken ct = default);

    // Додає запис у поточний Unit of Work — зберігається тим самим SaveChangesAsync, що й бізнес-зміни.
    void Add(Guid eventId, string eventType, DateTime processedAt);
}
