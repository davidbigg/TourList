using TourList.Shared.Enums;

namespace TourList.Shared.Entries;

/// <summary>An entry as returned to clients (browse map + admin management).</summary>
public sealed class EntryDto
{
    public Guid Id { get; init; }
    public Guid ListId { get; init; }
    public required string RawAddress { get; init; }
    public string? FormattedAddress { get; init; }
    public string? Notes { get; init; }
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public GeocodeStatus GeocodeStatus { get; init; }
    public string? GeocodeConfidence { get; init; }
    public bool ManuallyLocated { get; init; }
    public DateTime? LastGeocodedUtc { get; init; }
}
