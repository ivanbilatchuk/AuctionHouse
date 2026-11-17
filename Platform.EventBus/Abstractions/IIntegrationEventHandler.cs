namespace Platform.EventBus.Abstractions;

// Обробник одного типу події. Створюється в окремому DI-scope для кожного повідомлення,
// тож може залежати від scoped-сервісів (Unit of Work, DbContext, репозиторії).
// Обробник зобов'язаний бути ідемпотентним: повторна доставка тієї самої події (at-least-once) не змінює стан удруге.
public interface IIntegrationEventHandler<in TEvent> where TEvent : IntegrationEvent
{
    Task HandleAsync(TEvent @event, CancellationToken cancellationToken);
}
