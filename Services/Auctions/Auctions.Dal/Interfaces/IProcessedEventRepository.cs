namespace Auctions.Dal.Interfaces;

/// <summary>
/// Таблиця dbo.ProcessedEvents (Лаба 8): ідемпотентність споживача integration events.
/// Викликається в межах транзакції Unit of Work разом із бізнес-змінами.
/// </summary>
public interface IProcessedEventRepository
{
    /// <summary>Позначає подію обробленою. false — EventId уже є (повторна доставка), обробку слід пропустити.</summary>
    Task<bool> TryAddAsync(Guid eventId, string eventType, CancellationToken cancellationToken = default);
}
