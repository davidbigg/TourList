using System.ComponentModel.DataAnnotations;

namespace TourList.Shared.Entries;

public sealed class CreateEntryRequest
{
    [Required]
    [StringLength(500, MinimumLength = 1)]
    public string RawAddress { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Notes { get; set; }
}
