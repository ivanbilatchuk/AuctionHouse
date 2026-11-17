using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Platform.EventBus.Abstractions;
using Platform.EventBus.Diagnostics;
using Platform.EventBus.Serialization;
using RabbitMQ.Client;

namespace Platform.EventBus.RabbitMq;

// Publisher integration events. Singleton: IConnection один на застосунок (реєструє Aspire.RabbitMQ.Client),
// канал публікації — один, з publisher confirms: PublishAsync завершується, лише коли брокер підтвердив,
// що persistent-повідомлення прийнято (інакше — виняток). Канал не потокобезпечний для публікацій, тому SemaphoreSlim.
//
// Повідомлення: тіло — JSON (EventBusJson), BasicProperties:
//   Persistent = true (переживає перезапуск брокера в durable-черзі), ContentType = application/json,
//   MessageId = EventId, Type = назва типу події (за нею consumer обирає обробник), CorrelationId,
//   Timestamp, AppId = сервіс-джерело; заголовки x-correlation-id, x-schema-version, x-occurred-at + traceparent.
public sealed class RabbitMqEventPublisher(
    IConnection connection,
    EventBusMetrics metrics,
    IHostEnvironment environment,
    ILogger<RabbitMqEventPublisher> logger) : IEventPublisher, IAsyncDisposable
{
    private readonly SemaphoreSlim _channelLock = new(1, 1);
    private readonly ConcurrentDictionary<string, bool> _declaredExchanges = new();
    private IChannel? _channel;

    public async Task PublishAsync<TEvent>(TEvent @event, string exchange, string routingKey, CancellationToken cancellationToken = default)
        where TEvent : IntegrationEvent
    {
        var eventType = typeof(TEvent).Name;
        using var activity = TracePropagation.StartPublishActivity(exchange, routingKey, eventType, @event.EventId);
        activity?.SetTag("correlation.id", @event.CorrelationId);

        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = EventBusJson.ContentType,
            MessageId = @event.EventId.ToString(),
            Type = eventType,
            CorrelationId = @event.CorrelationId,
            AppId = environment.ApplicationName,
            Timestamp = new AmqpTimestamp(new DateTimeOffset(@event.OccurredAt, TimeSpan.Zero).ToUnixTimeSeconds()),
            Headers = new Dictionary<string, object?>
            {
                [MessageHeaders.CorrelationId] = @event.CorrelationId,
                [MessageHeaders.SchemaVersion] = @event.SchemaVersion,
                [MessageHeaders.OccurredAt] = @event.OccurredAt.ToString("O"),
                [MessageHeaders.SourceService] = environment.ApplicationName
            }
        };
        TracePropagation.Inject(properties);

        var body = JsonSerializer.SerializeToUtf8Bytes(@event, EventBusJson.Options);

        await _channelLock.WaitAsync(cancellationToken);
        try
        {
            var channel = await GetChannelAsync(cancellationToken);

            if (!_declaredExchanges.ContainsKey(exchange))
            {
                await EventBusTopology.DeclareExchangeAsync(channel, exchange, cancellationToken);
                _declaredExchanges[exchange] = true;
            }

            await channel.BasicPublishAsync(exchange, routingKey, mandatory: false, properties, body, cancellationToken);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            metrics.PublishFailed(eventType, exchange);
            logger.LogError(ex,
                "Failed to publish {EventType} {EventId} to {Exchange}/{RoutingKey}, CorrelationId {CorrelationId}",
                eventType, @event.EventId, exchange, routingKey, @event.CorrelationId);
            throw;
        }
        finally
        {
            _channelLock.Release();
        }

        metrics.Published(eventType, exchange);
        logger.LogInformation(
            "Published {EventType} {EventId} to {Exchange}/{RoutingKey} ({BodySize} bytes), CorrelationId {CorrelationId}",
            eventType, @event.EventId, exchange, routingKey, body.Length, @event.CorrelationId);
    }

    // Канал створюється ліниво й перестворюється, якщо його закрив брокер (наприклад, після помилки протоколу).
    private async Task<IChannel> GetChannelAsync(CancellationToken cancellationToken)
    {
        if (_channel is { IsOpen: true })
            return _channel;

        if (_channel is not null)
            await _channel.DisposeAsync();

        _declaredExchanges.Clear();
        _channel = await connection.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
            cancellationToken);
        return _channel;
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
            await _channel.DisposeAsync();
        _channelLock.Dispose();
    }
}
