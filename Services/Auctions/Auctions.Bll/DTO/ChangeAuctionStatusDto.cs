using System.ComponentModel.DataAnnotations;

namespace Auctions.Bll.DTO;

public class ChangeAuctionStatusDto
{
    [Required, StringLength(20, MinimumLength = 1)]
    public string Status { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Reason { get; set; }
}
