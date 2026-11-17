using System.Globalization;
using System.Text;

namespace Platform.EventBus.RabbitMq;

// Заголовки повідомлень платформи. Рядкові значення RabbitMQ.Client повертає як byte[] (UTF-8),
// цілі — як int/long, тож читання уніфіковано тут.
public static class MessageHeaders
{
    public const string CorrelationId = "x-correlation-id";
    public const string SchemaVersion = "x-schema-version";
    public const string OccurredAt = "x-occurred-at";
    public const string SourceService = "x-source-service";

    // Лічильник повторів: збільшується при кожній повторній публікації (повідомлення, повернене через requeue,
    // приходить без змін — заголовок у ньому змінити неможливо).
    public const string RetryCount = "x-retry-count";
    public const string LastError = "x-last-error";
    public const string OriginalExchange = "x-original-exchange";
    public const string OriginalRoutingKey = "x-original-routing-key";

    public static string? GetString(IDictionary<string, object?>? headers, string key) =>
        headers is not null && headers.TryGetValue(key, out var value) ? AsString(value) : null;

    public static int GetInt(IDictionary<string, object?>? headers, string key)
    {
        if (headers is null || !headers.TryGetValue(key, out var value) || value is null)
            return 0;

        return value switch
        {
            int i => i,
            long l => (int)l,
            short s => s,
            byte b => b,
            byte[] or string => int.TryParse(AsString(value), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0,
            _ => Convert.ToInt32(value, CultureInfo.InvariantCulture)
        };
    }

    public static string? AsString(object? value) => value switch
    {
        null => null,
        byte[] bytes => Encoding.UTF8.GetString(bytes),
        string s => s,
        _ => Convert.ToString(value, CultureInfo.InvariantCulture)
    };
}
