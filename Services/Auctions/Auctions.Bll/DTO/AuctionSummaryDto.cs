namespace Auctions.Bll.DTO;

public class AuctionSummaryDto
{
    public long AuctionId { get; set; }
    public long ItemId { get; set; }
    public string ItemTitle { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public decimal CurrentPrice { get; set; }
    public int BidCount { get; set; }
}
