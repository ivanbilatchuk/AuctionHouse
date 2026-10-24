namespace Auctions.Domain.Models;

public class Auction
{
    public long AuctionId { get; set; }
    public long ItemId { get; set; }
    public string ItemTitle { get; set; } = string.Empty;
    public Guid SellerExternalId { get; set; }
    public string Status { get; set; } = AuctionStatuses.Draft;
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public decimal StartingPrice { get; set; }
    public decimal CurrentPrice { get; set; }
    public int BidCount { get; set; }
    public long? LeadingBidderId { get; set; }

    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public AuctionDetails? Details { get; set; }
}
