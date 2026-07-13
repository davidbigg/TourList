using TourList.Shared.Navigation;

namespace TourList.Shared.Tests;

public sealed class ExternalMapsLinkTests
{
    [Fact]
    public void Google_link_with_origin_has_destination_origin_and_driving_mode()
    {
        var url = ExternalMapsLink.Build(MapsPlatform.Google, 47.6062, -122.3321, 47.61, -122.20);

        Assert.StartsWith("https://www.google.com/maps/dir/?api=1", url);
        Assert.Contains("destination=47.6062,-122.3321", url);
        Assert.Contains("origin=47.61,-122.2", url);
        Assert.Contains("travelmode=driving", url);
    }

    [Fact]
    public void Google_link_without_origin_omits_origin() // GPS denied — handoff still works
    {
        var url = ExternalMapsLink.Build(MapsPlatform.Google, 47.6062, -122.3321);

        Assert.Contains("destination=47.6062,-122.3321", url);
        Assert.DoesNotContain("origin=", url);
    }

    [Fact]
    public void Apple_link_uses_daddr_and_driving_flag()
    {
        var url = ExternalMapsLink.Build(MapsPlatform.Apple, 47.6062, -122.3321, 47.61, -122.20);

        Assert.StartsWith("https://maps.apple.com/?", url);
        Assert.Contains("daddr=47.6062,-122.3321", url);
        Assert.Contains("saddr=47.61,-122.2", url);
        Assert.Contains("dirflg=d", url);
    }

    [Theory]
    [InlineData("ios", MapsPlatform.Apple)]
    [InlineData("android", MapsPlatform.Google)]
    [InlineData("other", MapsPlatform.Google)]
    [InlineData(null, MapsPlatform.Google)]
    public void Platform_detection_picks_apple_only_for_ios(string? clientPlatform, MapsPlatform expected)
    {
        Assert.Equal(expected, ExternalMapsLink.PlatformFor(clientPlatform));
    }
}
