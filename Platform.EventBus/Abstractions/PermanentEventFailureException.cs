namespace Platform.EventBus.Abstractions;

// Постійний збій обробки: повтор не допоможе (некоректні дані, порушення бізнес-правила, невідомий тип події).
// Consumer одразу відхиляє таке повідомлення без requeue — воно потрапляє в DLQ для ручного розслідування.
public sealed class PermanentEventFailureException(string message, Exception? innerException = null)
    : Exception(message, innerException);
