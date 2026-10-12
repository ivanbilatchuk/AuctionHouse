using System.ComponentModel.DataAnnotations;

namespace Auctions.Bll.DTO;

public class RegisterForAuctionDto
{
    [Range(1, long.MaxValue)]
    public long AuctionId { get; set; }
}
