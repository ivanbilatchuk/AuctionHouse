namespace Auctions.Bll.DTO;

public class BidDto
{
    public long BidId { get; set; }
    public long AuctionId { get; set; }
    public long BidderId { get; set; }
    public decimal Amount { get; set; }
    public DateTime PlacedAt { get; set; }
    public bool IsWinning { get; set; }
}
