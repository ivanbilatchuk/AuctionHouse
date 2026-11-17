using System.Text.Json;
using System.Text.Json.Serialization;

namespace Platform.EventBus.Serialization;

// Єдиний формат повідомлень для publisher-ів і consumer-ів усіх сервісів:
// System.Text.Json, camelCase, enum — рядком, дати — ISO 8601 (UTC), невідомі поля ігноруються
// (старий споживач спокійно читає новішу версію події з доданими полями).
public static class EventBusJson
{
    public const string ContentType = "application/json";

    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip
    };
}
