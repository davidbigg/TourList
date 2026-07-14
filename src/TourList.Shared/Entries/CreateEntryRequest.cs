using System.ComponentModel.DataAnnotations;

namespace TourList.Shared.Entries;

public sealed class CreateEntryRequest
{
    [Required]
    [StringLength(500, MinimumLength = 1)]
    public required string RawAddress { get; init; }

    [StringLength(2000)]
    public string? Notes { get; init; }
}
