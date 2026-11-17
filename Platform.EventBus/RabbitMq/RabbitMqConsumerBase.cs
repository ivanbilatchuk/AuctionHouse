using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Platform.EventBus.Abstractions;
using Platform.EventBus.Diagnostics;
using Platform.EventBus.Serialization;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Platform.EventBus.RabbitMq;

// Базовий споживач подій: довготривалий BackgroundService з ручним підтвердженням (autoAck: false).
// Конкретний consumer сервісу (<Сервіс>.Api/IntegrationEvents/*Consumer.cs) задає exchange, чергу, bindings
// і підписки Subscribe<TEvent, THandler>(); реєструється в Program.cs через AddHostedService.
//
// Рішення щодо кожного повідомлення:
//   успіх                                    -> BasicAck (видаляється з черги);
//   постійний збій (JSON, невідомий тип,
//   бізнес-правило — IsPermanentFailure)     -> BasicNack(requeue: false) -> DLX -> <queue>.dlq;
//   тимчасовий збій (БД недоступна, таймаут)  -> повторна публікація в <queue>.retry з x-retry-count + 1
//                                               і затримкою (2, 4, 8 с), оригінал — BasicAck;
//   вичерпано MaxRetryAttempts               -> BasicNack(requeue: false) -> DLQ;
//   зупинка застосунку під час обробки        -> BasicNack(requeue: true): повідомлення повернеться в чергу.
// Повторна публікація, а не Nack(requeue: true): повернене в чергу повідомлення приходить без змін,
// лічильник у заголовку не збільшити — і збій повторювався б нескінченно без паузи.
public abstract class RabbitMqConsumerBase : BackgroundService
{
    private static readonly TimeSpan StartRetryDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan DeadLetterMonitorInterval = TimeSpan.FromSeconds(15);

    private readonly IServiceProvider _services;
    private readonly EventBusMetrics _metrics;
    private readonly Dictionary<string, Subscription> _subscriptions = new(StringComparer.Ordinal);

    private IChannel? _consumeChannel;
    private IChannel? _publishChannel;
    private string? _consumerTag;
    private CancellationToken _stoppingToken;
    private long _lastDeadLetterDepth;

    protected RabbitMqConsumerBase(IServiceProvider services, EventBusMetrics metrics, ILogger logger)
    {
        _services = services;
        _metrics = metrics;
        Logger = logger;
    }

    protected ILogger Logger { get; }

    // Exchange сервісу-джерела, черга цього сервісу та шаблони routing key.
    protected abstract string Exchange { get; }
    protected abstract string QueueName { get; }
    protected abstract IReadOnlyCollection<string> BindingKeys { get; }

    // Скільки непідтверджених повідомлень брокер може одночасно віддати споживачу.
    protected virtual ushort PrefetchCount => 10;

    // Максимум повторів тимчасового збою; далі — DLQ.
    protected virtual int MaxRetryAttempts => 3;

    // Експоненційна затримка перед повтором: 2, 4, 8 с.
    protected virtual TimeSpan RetryDelay(int attempt) => TimeSpan.FromSeconds(Math.Pow(2, attempt));

    // Постійні збої йдуть у DLQ одразу. Сервіс додає свої (доменні винятки, помилки валідації).
    protected virtual bool IsPermanentFailure(Exception exception) =>
        exception is JsonException or PermanentEventFailureException or NotSupportedException or FormatException;

    private string DeadLetterQueue => EventBusTopology.DeadLetterQueue(QueueName);

    // Маршрутизація за типом події (BasicProperties.Type = назва record-типу). Обробник створюється в scope повідомлення.
    protected void Subscribe<TEvent, THandler>()
        where TEvent : IntegrationEvent
        where THandler : IIntegrationEventHandler<TEvent>
    {
        _subscriptions[typeof(TEvent).Name] = new Subscription(
            Deserialize: body => JsonSerializer.Deserialize<TEvent>(body.Span, EventBusJson.Options)
                                 ?? throw new JsonException("Message body is empty (null)."),
            Handle: (provider, @event, ct) =>
                ActivatorUtilities.GetServiceOrCreateInstance<THandler>(provider).HandleAsync((TEvent)@event, ct));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _stoppingToken = stoppingToken;

        // Брокер може бути ще недоступний (або перезапускатися) — споживач не валить застосунок, а пробує знову.
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await StartConsumingAsync(stoppingToken);
                break;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Consumer for queue {Queue} could not start, retrying in {RetryDelay}", QueueName, StartRetryDelay);
                await DisposeChannelsAsync();
                try { await Task.Delay(StartRetryDelay, stoppingToken); }
                catch (OperationCanceledException) { return; }
            }
        }

        await MonitorDeadLetterQueueAsync(stoppingToken);
    }

    private async Task StartConsumingAsync(CancellationToken stoppingToken)
    {
        var connection = _services.GetRequiredService<IConnection>();

        _consumeChannel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
        // Окремий канал з publisher confirms — для повторної публікації: оригінал підтверджується (Ack),
        // лише коли брокер прийняв копію з новим лічильником.
        _publishChannel = await connection.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
            stoppingToken);

        await EventBusTopology.DeclareConsumerQueueAsync(_consumeChannel, Exchange, QueueName, BindingKeys, stoppingToken);
        await _consumeChannel.BasicQosAsync(prefetchSize: 0, prefetchCount: PrefetchCount, global: false, stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_consumeChannel);
        consumer.ReceivedAsync += OnMessageAsync;
        _consumerTag = await _consumeChannel.BasicConsumeAsync(QueueName, autoAck: false, consumer, stoppingToken);

        Logger.LogInformation(
            "Consumer started: queue {Queue} bound to {Exchange} with {BindingKeys}, prefetch {PrefetchCount}, handles {EventTypes}",
            QueueName, Exchange, BindingKeys, PrefetchCount, _subscriptions.Keys);
    }

    private async Task OnMessageAsync(object sender, BasicDeliverEventArgs delivery)
    {
        var properties = delivery.BasicProperties;
        var eventType = properties.Type ?? "unknown";
        var correlationId = properties.CorrelationId ?? MessageHeaders.GetString(properties.Headers, MessageHeaders.CorrelationId);
        var attempt = MessageHeaders.GetInt(properties.Headers, MessageHeaders.RetryCount);

        using var activity = TracePropagation.StartConsumerActivity(delivery, QueueName, eventType);
        activity?.SetTag("correlation.id", correlationId);
        activity?.SetTag("messaging.rabbitmq.retry_count", attempt);

        // Бізнес-контекст у кожному записі логу обробки (структуровані властивості для фільтрації в Dashboard).
        using var scope = Logger.BeginScope(new Dictionary<string, object?>
        {
            ["EventType"] = eventType,
            ["MessageId"] = properties.MessageId,
            ["CorrelationId"] = correlationId,
            ["Queue"] = QueueName,
            ["RetryCount"] = attempt
        });

        var startedAt = Stopwatch.GetTimestamp();
        IntegrationEvent? @event = null;
        string outcome;

        try
        {
            if (!_subscriptions.TryGetValue(eventType, out var subscription))
                throw new PermanentEventFailureException($"No handler is subscribed to event type '{eventType}' in queue '{QueueName}'.");

            @event = subscription.Deserialize(delivery.Body);
            Logger.LogInformation(
                "Received {EventType} {EventId} from {Exchange}/{RoutingKey} (attempt {Attempt}, occurred at {OccurredAt:O})",
                eventType, @event.EventId, delivery.Exchange, delivery.RoutingKey, attempt + 1, @event.OccurredAt);

            await using (var messageScope = _services.CreateAsyncScope())
                await subscription.Handle(messageScope.ServiceProvider, @event, _stoppingToken);

            await _consumeChannel!.BasicAckAsync(delivery.DeliveryTag, multiple: false);
            outcome = "ack";
            Logger.LogInformation("Acked {EventType} {EventId} in {ElapsedMs} ms",
                eventType, @event.EventId, Math.Round(Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds, 1));
        }
        catch (OperationCanceledException) when (_stoppingToken.IsCancellationRequested)
        {
            outcome = "requeue";
            Logger.LogWarning("Application is stopping: {EventType} {MessageId} returned to queue {Queue}",
                eventType, properties.MessageId, QueueName);
            await TryNackAsync(delivery, requeue: true);
        }
        catch (Exception ex) when (IsPermanentFailure(ex))
        {
            outcome = "dead_letter";
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            Logger.LogError(ex,
                "Permanent failure processing {EventType} {MessageId} from {Exchange}/{RoutingKey}: {Reason}. Sending to DLQ {DeadLetterQueue}",
                eventType, properties.MessageId, delivery.Exchange, delivery.RoutingKey, ex.Message, DeadLetterQueue);
            await TryNackAsync(delivery, requeue: false);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            outcome = await RetryOrDeadLetterAsync(delivery, ex, attempt, eventType);
        }

        _metrics.Processed(eventType, QueueName, outcome, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
    }

    private async Task<string> RetryOrDeadLetterAsync(BasicDeliverEventArgs delivery, Exception exception, int attempt, string eventType)
    {
        var properties = delivery.BasicProperties;

        if (attempt >= MaxRetryAttempts)
        {
            Logger.LogError(exception,
                "Message {MessageId} ({EventType}) failed after {Attempts} retries: {Reason}. Sending to DLQ {DeadLetterQueue}",
                properties.MessageId, eventType, attempt, exception.Message, DeadLetterQueue);
            await TryNackAsync(delivery, requeue: false);
            return "dead_letter";
        }

        var nextAttempt = attempt + 1;
        var delay = RetryDelay(nextAttempt);

        var headers = new Dictionary<string, object?>(properties.Headers ?? new Dictionary<string, object?>())
        {
            [MessageHeaders.RetryCount] = nextAttempt,
            [MessageHeaders.LastError] = Truncate($"{exception.GetType().Name}: {exception.Message}", 500)
        };
        // Після першого повтору повідомлення приходить уже з default exchange — зберігаємо оригінальну адресу.
        headers.TryAdd(MessageHeaders.OriginalExchange, delivery.Exchange);
        headers.TryAdd(MessageHeaders.OriginalRoutingKey, delivery.RoutingKey);

        var retryProperties = new BasicProperties(properties)
        {
            Headers = headers,
            Persistent = true,
            Expiration = ((long)delay.TotalMilliseconds).ToString(System.Globalization.CultureInfo.InvariantCulture)
        };

        try
        {
            // Default exchange ("") з routing key = ім'я черги: копія йде лише в чергу затримки ЦЬОГО сервісу,
            // а не в catalog-events (інакше подію повторно отримали б усі споживачі).
            await _publishChannel!.BasicPublishAsync(string.Empty, EventBusTopology.RetryQueue(QueueName),
                mandatory: true, retryProperties, delivery.Body, _stoppingToken);
            await _consumeChannel!.BasicAckAsync(delivery.DeliveryTag, multiple: false);
        }
        catch (Exception publishException)
        {
            // Не вдалося навіть перепублікувати — повертаємо оригінал у чергу (спроба не рахується).
            Logger.LogError(publishException, "Could not schedule retry of {MessageId} ({EventType}), returning it to queue {Queue}",
                properties.MessageId, eventType, QueueName);
            await TryNackAsync(delivery, requeue: true);
            return "requeue";
        }

        Logger.LogWarning(exception,
            "Transient failure processing {MessageId} ({EventType}): {Reason}. Retry {Attempt}/{MaxAttempts} in {RetryDelay}",
            properties.MessageId, eventType, exception.Message, nextAttempt, MaxRetryAttempts, delay);
        return "retry";
    }

    private async Task TryNackAsync(BasicDeliverEventArgs delivery, bool requeue)
    {
        try
        {
            await _consumeChannel!.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: requeue);
        }
        catch (Exception ex)
        {
            // Канал закрито — брокер сам поверне непідтверджене повідомлення в чергу (обробники ідемпотентні).
            Logger.LogWarning(ex, "Could not Nack {MessageId}: channel is closed, the broker will redeliver it",
                delivery.BasicProperties.MessageId);
        }
    }

    // Моніторинг глибини DLQ: gauge messaging.dlq.depth + Warning у лог, коли в DLQ з'являються нові повідомлення.
    private async Task MonitorDeadLetterQueueAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(DeadLetterMonitorInterval);
        do
        {
            try
            {
                if (_publishChannel is { IsOpen: true } channel)
                {
                    long depth = await channel.MessageCountAsync(DeadLetterQueue, stoppingToken);
                    _metrics.DeadLetterDepth(DeadLetterQueue, depth);

                    if (depth > _lastDeadLetterDepth)
                        Logger.LogWarning("Dead letter queue {DeadLetterQueue} has {Depth} messages (was {PreviousDepth}) — manual investigation required",
                            DeadLetterQueue, depth, _lastDeadLetterDepth);
                    _lastDeadLetterDepth = depth;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "Could not read depth of {DeadLetterQueue}", DeadLetterQueue);
            }
        }
        while (await WaitForNextTickAsync(timer, stoppingToken));
    }

    private static async Task<bool> WaitForNextTickAsync(PeriodicTimer timer, CancellationToken stoppingToken)
    {
        try { return await timer.WaitForNextTickAsync(stoppingToken); }
        catch (OperationCanceledException) { return false; }
    }

    // Коректна зупинка: скасувати підписку (нові повідомлення не надходять), дочекатися поточної обробки, закрити канали.
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_consumeChannel is { IsOpen: true } && _consumerTag is not null)
        {
            try { await _consumeChannel.BasicCancelAsync(_consumerTag, cancellationToken: cancellationToken); }
            catch (Exception ex) { Logger.LogDebug(ex, "Could not cancel consumer {ConsumerTag}", _consumerTag); }
        }

        await base.StopAsync(cancellationToken);
        await DisposeChannelsAsync();
        Logger.LogInformation("Consumer for queue {Queue} stopped", QueueName);
    }

    private async Task DisposeChannelsAsync()
    {
        foreach (var channel in new[] { _consumeChannel, _publishChannel })
        {
            if (channel is null) continue;
            try
            {
                if (channel.IsOpen) await channel.CloseAsync();
                await channel.DisposeAsync();
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "Error while closing channel of queue {Queue}", QueueName);
            }
        }

        _consumeChannel = null;
        _publishChannel = null;
        _consumerTag = null;
    }

    private static string Truncate(string value, int maxLength) => value.Length <= maxLength ? value : value[..maxLength];

    private sealed record Subscription(
        Func<ReadOnlyMemory<byte>, IntegrationEvent> Deserialize,
        Func<IServiceProvider, IntegrationEvent, CancellationToken, Task> Handle);
}
