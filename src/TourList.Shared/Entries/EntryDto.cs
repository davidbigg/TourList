using TourList.Shared.Enums;

namespace TourList.Shared.Entries;

/// <summary>An entry as returned to clients (browse map + admin management).</summary>
public sealed class EntryDto
{
    public Guid Id { get; set; }
    public Guid ListId { get; set; }
    public required string RawAddress { get; set; }
    public string? FormattedAddress { get; set; }
    public string? Notes { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public GeocodeStatus GeocodeStatus { get; set; }
    public string? GeocodeConfidence { get; set; }
    public bool ManuallyLocated { get; set; }
    public DateTime? LastGeocodedUtc { get; set; }
}
