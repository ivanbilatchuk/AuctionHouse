namespace Auctions.Domain.Models;

public class AuctionRegistration
{
    public long AuctionId { get; set; }
    public long BidderId { get; set; }
    public decimal DepositAmount { get; set; }
    public string Status { get; set; } = "Active";
    public DateTime RegisteredAt { get; set; }
}
