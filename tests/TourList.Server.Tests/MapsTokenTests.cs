using System.Net;
using TourList.Server.Tests.Infrastructure;

namespace TourList.Server.Tests;

[Collection(SqlServerCollection.Name)]
public sealed class MapsTokenTests(SqlServerContainerFixture fixture) : IDisposable
{
    private readonly TourListWebAppFactory _factory = new(
        fixture.ConnectionStringFor("tl_maps_tests"),
        adminEmail: "admin@tourlist.local",
        adminPassword: "Admin!Passw0rd");

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Token_endpoint_is_guest_reachable_and_returns_503_when_disabled()
    {
        var client = _factory.NewClient();

        var response = await client.GetAsync("/api/maps/token");

        // Guest-reachable (no 401) and gracefully unavailable until Azure Maps is configured.
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }
}
