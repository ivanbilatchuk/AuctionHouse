using System.ComponentModel.DataAnnotations;

namespace Auctions.Bll.DTO;

public class UpdateAuctionDto
{
    [Required, StringLength(200, MinimumLength = 1)]
    public string ItemTitle { get; set; } = string.Empty;

    [Required]
    public DateTime StartsAt { get; set; }

    [Required]
    public DateTime EndsAt { get; set; }

    [Range(0.01, 1_000_000_000)]
    public decimal StartingPrice { get; set; }

    [Range(0.01, 1_000_000_000)]
    public decimal? ReservePrice { get; set; }

    [Range(0.01, 1_000_000_000)]
    public decimal BidIncrement { get; set; } = 1;

    [Range(0, 1_000_000_000)]
    public decimal RequiredDeposit { get; set; }

    [Range(0, 100)]
    public decimal BuyerPremiumPercent { get; set; } = 10;

    [StringLength(1000)]
    public string? Notes { get; set; }

    [Required, MinLength(1)]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
