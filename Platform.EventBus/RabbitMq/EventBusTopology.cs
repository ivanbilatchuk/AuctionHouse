using RabbitMQ.Client;

namespace Platform.EventBus.RabbitMq;

// Декларативна топологія RabbitMQ. Оголошується в коді під час старту (publisher — свій exchange,
// consumer — exchange джерела + свої черги). Повторне оголошення з тими самими параметрами безпечне;
// параметри вже створених черг не змінюються вручну через Management UI — інакше PRECONDITION_FAILED на старті.
//
//   <source>-events        topic   exchange сервісу-власника; routing key = бізнес-факт (item.updated, auction.closed)
//   <queue>                 durable черга сервісу-споживача з bindings за шаблонами (* — одне слово, # — будь-яка кількість);
//                           x-dead-letter-exchange = <source>-events.dlx, x-dead-letter-routing-key = <queue>
//   <source>-events.dlx     direct  dead letter exchange: відхилені повідомлення (Nack без requeue) — за ім'ям черги
//   <queue>.dlq             durable черга «мертвих» повідомлень для ручного розслідування (x-death з причиною)
//   <queue>.retry           durable черга затримки повторів: без споживачів; повідомлення живе Expiration мс,
//                           після чого RabbitMQ повертає його в <queue> (DLX = default exchange, routing key = <queue>)
//
// DLX має тип direct, а не fanout: на catalog-events підписано кілька черг, і з fanout кожна DLQ
// отримувала б «мертві» повідомлення всіх споживачів.
public static class EventBusTopology
{
    // Exchanges сервісів-власників даних.
    public const string CatalogEvents = "catalog-events";
    public const string AuctionsEvents = "auctions-events";

    public static string DeadLetterExchange(string exchange) => $"{exchange}.dlx";

    public static string DeadLetterQueue(string queue) => $"{queue}.dlq";

    public static string RetryQueue(string queue) => $"{queue}.retry";

    public static Task DeclareExchangeAsync(IChannel channel, string exchange, CancellationToken cancellationToken) =>
        channel.ExchangeDeclareAsync(exchange, ExchangeType.Topic, durable: true, autoDelete: false,
            cancellationToken: cancellationToken);

    public static async Task DeclareConsumerQueueAsync(
        IChannel channel, string exchange, string queue, IEnumerable<string> bindingKeys, CancellationToken cancellationToken)
    {
        var deadLetterExchange = DeadLetterExchange(exchange);
        var deadLetterQueue = DeadLetterQueue(queue);
        var retryQueue = RetryQueue(queue);

        await DeclareExchangeAsync(channel, exchange, cancellationToken);
        await channel.ExchangeDeclareAsync(deadLetterExchange, ExchangeType.Direct, durable: true, autoDelete: false,
            cancellationToken: cancellationToken);

        // Основна черга: відхилене повідомлення -> DLX з routing key = ім'я черги -> лише своя DLQ.
        await channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = deadLetterExchange,
                ["x-dead-letter-routing-key"] = queue
            },
            cancellationToken: cancellationToken);

        foreach (var bindingKey in bindingKeys)
            await channel.QueueBindAsync(queue, exchange, bindingKey, cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(deadLetterQueue, durable: true, exclusive: false, autoDelete: false,
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(deadLetterQueue, deadLetterExchange, routingKey: queue,
            cancellationToken: cancellationToken);

        // Черга затримки: прострочене повідомлення йде в default exchange ("") з routing key = основна черга.
        await channel.QueueDeclareAsync(retryQueue, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = "",
                ["x-dead-letter-routing-key"] = queue
            },
            cancellationToken: cancellationToken);
    }
}
