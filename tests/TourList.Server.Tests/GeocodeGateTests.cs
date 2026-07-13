using System.Text.Json;
using TourList.Server.Services.Geocoding;

namespace TourList.Server.Tests;

/// <summary>
/// Pure tests of the geocoding acceptance gate + JSON mapping. Critically verifies the GeoJSON
/// <c>[longitude, latitude]</c> ordering is mapped to Latitude/Longitude correctly.
/// </summary>
public sealed class GeocodeGateTests
{
    private static GeocodingResponse Parse(string json) =>
        JsonSerializer.Deserialize<GeocodingResponse>(json)!;

    private static string Feature(string confidence, string matchCode, string type, double lon, double lat) => $$"""
        {
          "type": "FeatureCollection",
          "features": [{
            "type": "Feature",
            "geometry": { "type": "Point", "coordinates": [{{lon}}, {{lat}}] },
            "properties": {
              "type": "{{type}}",
              "confidence": "{{confidence}}",
              "matchCodes": ["{{matchCode}}"],
              "address": { "formattedAddress": "1 Microsoft Way, Redmond, WA 98052" }
            }
          }]
        }
        """;

    [Fact]
    public void High_confidence_good_address_is_located_with_lon_lat_mapped_correctly()
    {
        var response = Parse(Feature("High", "Good", "Address", lon: -122.12939, lat: 47.64263));

        var result = GeocodeGate.Evaluate(response);

        Assert.Equal(GeocodeOutcome.Located, result.Outcome);
        // GeoJSON is [lon, lat]; ensure they are not swapped.
        Assert.Equal(47.64263, result.Latitude);
        Assert.Equal(-122.12939, result.Longitude);
        Assert.Equal("High", result.Confidence);
        Assert.Equal("1 Microsoft Way, Redmond, WA 98052", result.FormattedAddress);
    }

    [Fact]
    public void Medium_confidence_good_address_is_accepted()
    {
        var result = GeocodeGate.Evaluate(Parse(Feature("Medium", "Good", "Address", 1.0, 2.0)));
        Assert.Equal(GeocodeOutcome.Located, result.Outcome);
    }

    [Fact]
    public void Low_confidence_is_unlocatable()
    {
        var result = GeocodeGate.Evaluate(Parse(Feature("Low", "Good", "Address", 1.0, 2.0)));
        Assert.Equal(GeocodeOutcome.Unlocatable, result.Outcome);
    }

    [Fact]
    public void Up_hierarchy_non_address_type_is_unlocatable()
    {
        var result = GeocodeGate.Evaluate(Parse(Feature("High", "UpHierarchy", "PopulatedPlace", 1.0, 2.0)));
        Assert.Equal(GeocodeOutcome.Unlocatable, result.Outcome);
    }

    [Fact]
    public void Good_match_but_non_address_type_is_unlocatable()
    {
        var result = GeocodeGate.Evaluate(Parse(Feature("High", "Good", "RoadBlock", 1.0, 2.0)));
        Assert.Equal(GeocodeOutcome.Unlocatable, result.Outcome);
    }

    [Fact]
    public void Empty_feature_collection_is_unlocatable()
    {
        var result = GeocodeGate.Evaluate(Parse("""{ "type": "FeatureCollection", "features": [] }"""));
        Assert.Equal(GeocodeOutcome.Unlocatable, result.Outcome);
    }
}
