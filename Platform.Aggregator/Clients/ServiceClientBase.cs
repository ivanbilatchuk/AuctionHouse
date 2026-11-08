using System.Net;
using System.Text.Json;

namespace Platform.Aggregator.Clients;

// Спільна частина типізованих клієнтів: GET + десеріалізація System.Text.Json (camelCase, як віддають сервіси).
public abstract class ServiceClientBase(HttpClient httpClient, ILogger logger)
{
    protected static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // null — якщо сервіс відповів 404; інші неуспішні коди -> HttpRequestException (обробляє SafeAsync).
    protected async Task<T?> GetAsync<T>(string relativeUrl, CancellationToken ct) where T : class
    {
        logger.LogDebug("GET {BaseAddress}{Url}", httpClient.BaseAddress, relativeUrl);

        using var response = await httpClient.GetAsync(relativeUrl, ct);
        logger.LogDebug("GET {Url} returned {StatusCode}", relativeUrl, (int)response.StatusCode);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, ct);
    }
}
