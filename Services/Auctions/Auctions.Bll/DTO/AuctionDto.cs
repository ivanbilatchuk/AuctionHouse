namespace Auctions.Bll.DTO;

public class AuctionDto
{
    public long AuctionId { get; set; }
    public long ItemId { get; set; }
    public string ItemTitle { get; set; } = string.Empty;
    public Guid SellerExternalId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public decimal StartingPrice { get; set; }
    public decimal CurrentPrice { get; set; }
    public int BidCount { get; set; }
    public long? LeadingBidderId { get; set; }
    public decimal? ReservePrice { get; set; }
    public decimal BidIncrement { get; set; }
    public decimal RequiredDeposit { get; set; }
    public decimal BuyerPremiumPercent { get; set; }
    public int AntiSnipingWindowMinutes { get; set; }
    public int AntiSnipingExtensionMinutes { get; set; }
    public string? Notes { get; set; }

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
