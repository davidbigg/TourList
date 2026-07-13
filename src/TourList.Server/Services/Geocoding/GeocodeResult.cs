namespace TourList.Server.Services.Geocoding;

public enum GeocodeOutcome
{
    /// <summary>Accepted match; coordinates are set.</summary>
    Located,

    /// <summary>Tried, but no match passed the confidence/match-code/type gate. Flag for admin.</summary>
    Unlocatable,

    /// <summary>Geocoding is disabled/not configured; leave the entry Pending (not a failure).</summary>
    Skipped,
}

/// <summary>Result of a geocoding attempt. Coordinates are only meaningful when <see cref="Outcome"/> is Located.</summary>
public sealed record GeocodeResult(
    GeocodeOutcome Outcome,
    double? Latitude,
    double? Longitude,
    string? FormattedAddress,
    string? Confidence,
    string? Reason)
{
    public static GeocodeResult Located(double latitude, double longitude, string? formattedAddress, string? confidence) =>
        new(GeocodeOutcome.Located, latitude, longitude, formattedAddress, confidence, null);

    public static GeocodeResult Unlocatable(string reason) =>
        new(GeocodeOutcome.Unlocatable, null, null, null, null, reason);

    public static readonly GeocodeResult Skipped =
        new(GeocodeOutcome.Skipped, null, null, null, null, "Geocoding is not configured.");
}
