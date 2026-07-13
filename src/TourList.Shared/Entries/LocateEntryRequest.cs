using System.ComponentModel.DataAnnotations;

namespace TourList.Shared.Entries;

/// <summary>
/// Admin resolution of a flagged entry: either drop a manual pin (Latitude + Longitude) or
/// supply a corrected address to re-geocode. Exactly one path should be provided.
/// </summary>
public sealed class LocateEntryRequest
{
    [Range(-90, 90)]
    public double? Latitude { get; set; }

    [Range(-180, 180)]
    public double? Longitude { get; set; }

    [StringLength(500)]
    public string? CorrectedAddress { get; set; }

    public bool HasManualPin => Latitude.HasValue && Longitude.HasValue;
    public bool HasCorrectedAddress => !string.IsNullOrWhiteSpace(CorrectedAddress);
}
