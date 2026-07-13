namespace TourList.Server.Services.Geocoding;

/// <summary>
/// The geocoding acceptance gate (pure, unit-tested). Accepts the top match only when confidence is
/// High or Medium, the match codes include <c>Good</c>, and the result type is <c>Address</c>;
/// anything else is Unlocatable (flagged for admin). Also performs the deliberate GeoJSON
/// <c>[longitude, latitude]</c> → Latitude/Longitude mapping.
/// </summary>
public static class GeocodeGate
{
    private static readonly HashSet<string> AcceptedConfidence =
        new(StringComparer.OrdinalIgnoreCase) { "High", "Medium" };

    private const string RequiredMatchCode = "Good";
    private const string RequiredType = "Address";

    public static GeocodeResult Evaluate(GeocodingResponse? response)
    {
        var feature = response?.Features?.FirstOrDefault();
        if (feature is null)
        {
            return GeocodeResult.Unlocatable("No geocoding match was returned.");
        }

        var coords = feature.Geometry?.Coordinates;
        if (coords is not { Length: >= 2 })
        {
            return GeocodeResult.Unlocatable("Match did not include coordinates.");
        }

        var props = feature.Properties;
        var confidence = props?.Confidence;
        var type = props?.Type;
        var matchCodes = props?.MatchCodes ?? [];

        if (confidence is null || !AcceptedConfidence.Contains(confidence))
        {
            return GeocodeResult.Unlocatable($"Confidence '{confidence ?? "none"}' is below the High/Medium threshold.");
        }

        if (!matchCodes.Any(m => string.Equals(m, RequiredMatchCode, StringComparison.OrdinalIgnoreCase)))
        {
            return GeocodeResult.Unlocatable($"Match code does not include '{RequiredMatchCode}'.");
        }

        if (!string.Equals(type, RequiredType, StringComparison.OrdinalIgnoreCase))
        {
            return GeocodeResult.Unlocatable($"Result type '{type ?? "none"}' is not '{RequiredType}'.");
        }

        // GeoJSON coordinate order is [longitude, latitude] — reversed from intuition.
        var longitude = coords[0];
        var latitude = coords[1];
        return GeocodeResult.Located(latitude, longitude, props?.Address?.FormattedAddress, confidence);
    }
}
