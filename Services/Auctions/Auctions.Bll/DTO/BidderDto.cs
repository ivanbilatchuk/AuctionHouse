namespace Auctions.Bll.DTO;

public class BidderDto
{
    public long BidderId { get; set; }
    public Guid ExternalUserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public decimal WalletBalance { get; set; }
    public bool IsBlocked { get; set; }
}
