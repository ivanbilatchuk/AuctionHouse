using Platform.Aggregator.Dto;

namespace Platform.Aggregator.Clients;

// Типізований клієнт Community API (BaseAddress = https+http://community-api).
public sealed class CommunityClient(HttpClient httpClient, ILogger<CommunityClient> logger)
    : ServiceClientBase(httpClient, logger)
{
    public const string ServiceName = "community";

    public async Task<IReadOnlyList<ExpertOpinionDto>> GetExpertOpinionsAsync(long itemId, CancellationToken ct)
    {
        var page = await GetAsync<ExpertOpinionPageDto>($"/api/expert-opinions?itemId={itemId}&pageSize=50", ct);
        return page?.Items ?? [];
    }
}
