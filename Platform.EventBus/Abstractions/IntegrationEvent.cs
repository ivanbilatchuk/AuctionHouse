using System.Text.Json.Serialization;

namespace Platform.EventBus.Abstractions;

// Базовий тип усіх integration events платформи.
// record: незмінний (init-only) і з порівнянням за значенням; подія самодостатня — споживач не звертається
// до сервісу-власника по додаткові дані.
//   EventId       — унікальний ідентифікатор (Guid): ключ ідемпотентності в таблиці оброблених подій споживача,
//                   MessageId повідомлення RabbitMQ;
//   OccurredAt    — момент бізнес-зміни (UTC): споживач не перезаписує новіші дані старішою подією (out-of-order);
//   CorrelationId — X-Correlation-Id HTTP-запиту, який спричинив подію (бізнес-трасування, Лаба 5).
public abstract record IntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();

    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;

    public string? CorrelationId { get; init; }

    // Версія схеми — у заголовку x-schema-version, не в тілі. Правило еволюції: нові поля лише додаються,
    // наявні не перейменовуються і не видаляються; за несумісної зміни — новий тип події.
    [JsonIgnore]
    public virtual int SchemaVersion => 1;
}
