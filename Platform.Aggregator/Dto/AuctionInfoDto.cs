namespace Platform.Aggregator.Dto;

// Відповідь Auctions API GET /api/auctions/{id} (лише поля, потрібні картці; RowVersion і SellerExternalId не передаємо далі).
public sealed class AuctionInfoDto
{
    public long AuctionId { get; init; }
    public long ItemId { get; init; }
    public string ItemTitle { get; init; } = "";
    public string Status { get; init; } = "";
    public DateTime StartsAt { get; init; }
    public DateTime EndsAt { get; init; }
    public decimal StartingPrice { get; init; }
    public decimal CurrentPrice { get; init; }
    public int BidCount { get; init; }
    public long? LeadingBidderId { get; init; }
    public decimal? ReservePrice { get; init; }
    public decimal BidIncrement { get; init; }
    public decimal BuyerPremiumPercent { get; init; }
}

// Відповідь Auctions API GET /api/auctions/{id}/bids.
public sealed class BidDto
{
    public long BidId { get; init; }
    public long BidderId { get; init; }
    public decimal Amount { get; init; }
    public DateTime PlacedAt { get; init; }
    public bool IsWinning { get; init; }
}
