using System.ComponentModel.DataAnnotations;

namespace Catalog.Bll.DTO;

public sealed class ChangeItemStatusDto
{
    [Required]
    public string Status { get; init; } = "";
}
