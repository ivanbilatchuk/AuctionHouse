namespace Catalog.Dal.Entities;

// Запис про оброблений integration event (Лаба 8, ідемпотентність споживача).
// Додається в той самий SaveChangesAsync, що й бізнес-зміни, — тобто в одній транзакції:
// або подію застосовано й позначено обробленою, або ні те, ні інше.
public sealed class ProcessedEvent
{
    private ProcessedEvent() { }

    public ProcessedEvent(Guid eventId, string eventType, DateTime processedAt)
    {
        EventId = eventId;
        EventType = eventType;
        ProcessedAt = processedAt;
    }

    public Guid EventId { get; private set; }

    public string EventType { get; private set; } = null!;

    public DateTime ProcessedAt { get; private set; }
}
