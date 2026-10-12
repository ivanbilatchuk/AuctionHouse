namespace Auctions.Domain.Models;

public class AuctionDetails
{
    public long AuctionId { get; set; }
    public decimal? ReservePrice { get; set; }
    public decimal BidIncrement { get; set; } = 1;
    public decimal RequiredDeposit { get; set; }
    public decimal BuyerPremiumPercent { get; set; } = 10;
    public int AntiSnipingWindowMinutes { get; set; } = 2;
    public int AntiSnipingExtensionMinutes { get; set; } = 2;
    public string? Notes { get; set; }
}
