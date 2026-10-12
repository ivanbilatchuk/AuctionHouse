using System.ComponentModel.DataAnnotations;

namespace Auctions.Bll.DTO;

public class PlaceBidDto
{
    [Range(1, long.MaxValue)]
    public long BidderId { get; set; }

    [Range(0.01, 1_000_000_000)]
    public decimal Amount { get; set; }
}
