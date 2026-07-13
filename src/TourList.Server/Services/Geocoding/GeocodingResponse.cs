using System.Text.Json.Serialization;

namespace TourList.Server.Services.Geocoding;

/// <summary>
/// Minimal typed view of the Azure Maps Get Geocoding GeoJSON response. Coordinates are
/// <b>[longitude, latitude]</b> per the GeoJSON spec — see <see cref="GeocodeGate"/> for the mapping.
/// </summary>
public sealed class GeocodingResponse
{
    [JsonPropertyName("features")]
    public List<GeocodingFeature>? Features { get; set; }
}

public sealed class GeocodingFeature
{
    [JsonPropertyName("geometry")]
    public GeocodingGeometry? Geometry { get; set; }

    [JsonPropertyName("properties")]
    public GeocodingProperties? Properties { get; set; }
}

public sealed class GeocodingGeometry
{
    /// <summary>GeoJSON point as [longitude, latitude].</summary>
    [JsonPropertyName("coordinates")]
    public double[]? Coordinates { get; set; }
}

public sealed class GeocodingProperties
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("confidence")]
    public string? Confidence { get; set; }

    [JsonPropertyName("matchCodes")]
    public List<string>? MatchCodes { get; set; }

    [JsonPropertyName("address")]
    public GeocodingAddress? Address { get; set; }
}

public sealed class GeocodingAddress
{
    [JsonPropertyName("formattedAddress")]
    public string? FormattedAddress { get; set; }
}
