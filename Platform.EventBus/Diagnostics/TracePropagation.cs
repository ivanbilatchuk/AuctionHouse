using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Platform.EventBus.RabbitMq;

namespace Platform.EventBus.Diagnostics;

// Передача контексту трасування через брокер (W3C Trace Context: заголовки traceparent / tracestate + baggage).
// Publisher: спан Producer "<exchange> publish" і його контекст — у заголовки повідомлення.
// Consumer: контекст із заголовків -> спан Consumer "<queue> process" як нащадок спану publish.
// Разом: HTTP-запит -> publish -> process в іншому сервісі -> запити до БД — один trace в Aspire Dashboard.
// ActivitySource "Platform.Messaging" підключено в Platform.ServiceDefaults (AddSource).
// Вбудовану трасу RabbitMQ.Client вимкнено (DisableTracing у EventBusExtensions), щоб механізм було видно явно.
public static class TracePropagation
{
    public const string ActivitySourceName = "Platform.Messaging";

    public static readonly ActivitySource Source = new(ActivitySourceName);

    private static TextMapPropagator Propagator => Propagators.DefaultTextMapPropagator;

    public static Activity? StartPublishActivity(string exchange, string routingKey, string eventType, Guid eventId)
    {
        var activity = Source.StartActivity($"{exchange} publish", ActivityKind.Producer);
        if (activity is null)
            return null;

        activity.SetTag("messaging.system", "rabbitmq");
        activity.SetTag("messaging.operation.type", "publish");
        activity.SetTag("messaging.destination.name", exchange);
        activity.SetTag("messaging.rabbitmq.destination.routing_key", routingKey);
        activity.SetTag("messaging.message.id", eventId.ToString());
        activity.SetTag("event.type", eventType);
        return activity;
    }

    // Записує контекст поточного Activity (спан publish) у заголовки повідомлення.
    public static void Inject(BasicProperties properties)
    {
        if (Activity.Current is not { } activity)
            return;

        properties.Headers ??= new Dictionary<string, object?>();
        Propagator.Inject(new PropagationContext(activity.Context, Baggage.Current), properties.Headers,
            static (headers, key, value) => headers[key] = value);
    }

    // Відновлює контекст із заголовків (значення приходять як byte[]) і стартує спан Consumer як його нащадок.
    public static Activity? StartConsumerActivity(BasicDeliverEventArgs delivery, string queue, string eventType)
    {
        var parent = Propagator.Extract(default, delivery.BasicProperties.Headers,
            static (headers, key) => headers is not null && headers.TryGetValue(key, out var value) && MessageHeaders.AsString(value) is { } text
                ? [text]
                : []);
        Baggage.Current = parent.Baggage;

        var activity = Source.StartActivity($"{queue} process", ActivityKind.Consumer, parent.ActivityContext);
        if (activity is null)
            return null;

        activity.SetTag("messaging.system", "rabbitmq");
        activity.SetTag("messaging.operation.type", "process");
        activity.SetTag("messaging.destination.name", queue);
        activity.SetTag("messaging.rabbitmq.destination.routing_key", delivery.RoutingKey);
        activity.SetTag("messaging.message.id", delivery.BasicProperties.MessageId);
        activity.SetTag("event.type", eventType);
        return activity;
    }
}
