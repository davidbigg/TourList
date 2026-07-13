using TourList.Shared.Enums;

namespace TourList.Server.Data.Entities;

/// <summary>
/// One address within a list. Coordinates are stored (geocoded), not re-derived per view.
/// Editing the address re-geocodes; low-confidence results are flagged <see cref="GeocodeStatus.Unlocatable"/>.
/// </summary>
public class Entry
{
    public Guid Id { get; set; }

    public Guid ListId { get; set; }

    /// <summary>The address as entered/imported.</summary>
    public string RawAddress { get; set; } = string.Empty;

    /// <summary>The normalized address returned by geocoding (when located).</summary>
    public string? FormattedAddress { get; set; }

    public string? Notes { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public GeocodeStatus GeocodeStatus { get; set; } = GeocodeStatus.Pending;

    /// <summary>Azure Maps confidence bucket for the accepted match (e.g. "High"/"Medium").</summary>
    public string? GeocodeConfidence { get; set; }

    /// <summary>True when an admin placed/corrected the location by hand via <c>/locate</c>.</summary>
    public bool ManuallyLocated { get; set; }

    public DateTime? LastGeocodedUtc { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;

    public AddressList? List { get; set; }
}
