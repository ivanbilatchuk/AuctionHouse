namespace Auctions.Bll.DTO;

public class RegistrationDto
{
    public long AuctionId { get; set; }
    public long BidderId { get; set; }
    public decimal DepositAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime RegisteredAt { get; set; }
}
