namespace TourList.Server.Services.Geocoding;

/// <summary>Resolves a free-text address to coordinates, applying the acceptance gate.</summary>
public interface IGeocoder
{
    Task<GeocodeResult> GeocodeAsync(string address, CancellationToken ct = default);
}
