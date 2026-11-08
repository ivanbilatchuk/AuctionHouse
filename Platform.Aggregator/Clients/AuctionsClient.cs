using Platform.Aggregator.Dto;

namespace Platform.Aggregator.Clients;

// Типізований клієнт Auctions API (BaseAddress = https+http://auctions-api, розв'язує service discovery).
public sealed class AuctionsClient(HttpClient httpClient, ILogger<AuctionsClient> logger)
    : ServiceClientBase(httpClient, logger)
{
    public const string ServiceName = "auctions";

    public Task<AuctionInfoDto?> GetAuctionAsync(long auctionId, CancellationToken ct)
        => GetAsync<AuctionInfoDto>($"/api/auctions/{auctionId}", ct);

    public async Task<IReadOnlyList<BidDto>> GetBidsAsync(long auctionId, CancellationToken ct)
        => await GetAsync<List<BidDto>>($"/api/auctions/{auctionId}/bids", ct) ?? [];
}
