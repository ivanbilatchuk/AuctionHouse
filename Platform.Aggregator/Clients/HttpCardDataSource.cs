using Platform.Aggregator.Dto;

namespace Platform.Aggregator.Clients;

// Режим ?transport=http: типізовані HTTP-клієнти з Лаби 5 (REST + JSON).
public sealed class HttpCardDataSource(AuctionsClient auctions, CatalogClient catalog, CommunityClient community) : ICardDataSource
{
    public const string Name = "http";

    public string Transport => Name;

    public Task<AuctionInfoDto?> GetAuctionAsync(long auctionId, CancellationToken ct) => auctions.GetAuctionAsync(auctionId, ct);

    public Task<LotDto?> GetLotAsync(long itemId, CancellationToken ct) => catalog.GetItemAsync(itemId, ct);

    public Task<IReadOnlyList<ExpertOpinionDto>> GetExpertOpinionsAsync(long itemId, CancellationToken ct)
        => community.GetExpertOpinionsAsync(itemId, ct);

    public async Task<IReadOnlyList<BidDto>> GetRecentBidsAsync(long auctionId, int limit, CancellationToken ct)
        => (await auctions.GetBidsAsync(auctionId, ct)).OrderByDescending(b => b.PlacedAt).Take(limit).ToList();
}
