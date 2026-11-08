using Platform.Aggregator.Dto;

namespace Platform.Aggregator.Clients;

// Типізований клієнт Catalog API (BaseAddress = https+http://catalog-api).
public sealed class CatalogClient(HttpClient httpClient, ILogger<CatalogClient> logger)
    : ServiceClientBase(httpClient, logger)
{
    public const string ServiceName = "catalog";

    public Task<LotDto?> GetItemAsync(long itemId, CancellationToken ct)
        => GetAsync<LotDto>($"/api/items/{itemId}", ct);
}
