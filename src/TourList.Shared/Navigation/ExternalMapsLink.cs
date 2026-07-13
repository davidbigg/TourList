using System.Globalization;

namespace TourList.Shared.Navigation;

public enum MapsPlatform
{
    Google,
    Apple,
}

/// <summary>
/// Builds turn-by-turn deep links for the external maps app (one destination per tap). Origin is
/// the driver's current location when available, and omitted otherwise (GPS denied) — the handoff
/// still works. Coordinates are formatted invariantly so decimals never localize.
/// </summary>
public static class ExternalMapsLink
{
    public static string Build(
        MapsPlatform platform,
        double destinationLatitude,
        double destinationLongitude,
        double? originLatitude = null,
        double? originLongitude = null)
    {
        var destination = Coord(destinationLatitude, destinationLongitude);
        var hasOrigin = originLatitude.HasValue && originLongitude.HasValue;
        var origin = hasOrigin ? Coord(originLatitude!.Value, originLongitude!.Value) : null;

        return platform switch
        {
            MapsPlatform.Apple =>
                $"https://maps.apple.com/?daddr={destination}&dirflg=d" + (hasOrigin ? $"&saddr={origin}" : string.Empty),
            _ =>
                $"https://www.google.com/maps/dir/?api=1&destination={destination}&travelmode=driving" + (hasOrigin ? $"&origin={origin}" : string.Empty),
        };
    }

    /// <summary>iOS devices → Apple Maps; everything else → Google Maps (universal cross-platform URL).</summary>
    public static MapsPlatform PlatformFor(string? clientPlatform) =>
        string.Equals(clientPlatform, "ios", StringComparison.OrdinalIgnoreCase)
            ? MapsPlatform.Apple
            : MapsPlatform.Google;

    private static string Coord(double lat, double lng) =>
        string.Create(CultureInfo.InvariantCulture, $"{lat},{lng}");
}
