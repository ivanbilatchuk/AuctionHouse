using System.ComponentModel.DataAnnotations;

namespace Auctions.Bll.DTO;

public class CreateBidderDto
{
    [Required]
    public Guid ExternalUserId { get; set; }

    [Required, StringLength(100, MinimumLength = 2)]
    public string DisplayName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Range(0, 1_000_000_000)]
    public decimal InitialBalance { get; set; }
}
