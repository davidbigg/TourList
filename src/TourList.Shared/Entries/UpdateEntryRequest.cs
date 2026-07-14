using System.ComponentModel.DataAnnotations;

namespace TourList.Shared.Entries;

public sealed class UpdateEntryRequest
{
    [Required]
    [StringLength(500, MinimumLength = 1)]
    public required string RawAddress { get; set; }

    [StringLength(2000)]
    public string? Notes { get; set; }
}
