using System.Net.Http.Json;
using System.Text.Json;
using Azure.Identity;
using Microsoft.Extensions.Options;
using TourList.Server.Options;

namespace TourList.Server.Services.Geocoding;

/// <summary>
/// Calls the Azure Maps Get Geocoding REST API via a typed <see cref="HttpClient"/> and applies
/// <see cref="GeocodeGate"/>. When Azure Maps is disabled, returns <see cref="GeocodeResult.Skipped"/>
/// without making a request.
/// </summary>
public sealed class AzureMapsGeocoder(
    HttpClient http,
    IOptions<AzureMapsOptions> options,
    ILogger<AzureMapsGeocoder> logger) : IGeocoder
{
    private readonly AzureMapsOptions _options = options.Value;

    public async Task<GeocodeResult> GeocodeAsync(string address, CancellationToken ct = default)
    {
        if (!_options.Enabled)
        {
            return GeocodeResult.Skipped;
        }

        if (string.IsNullOrWhiteSpace(address))
        {
            return GeocodeResult.Unlocatable("Address is empty.");
        }

        var url = $"geocode?api-version={Uri.EscapeDataString(_options.ApiVersion)}&top=1&query={Uri.EscapeDataString(address)}";

        try
        {
            using var response = await http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Azure Maps geocode returned {Status} for {Address}.", response.StatusCode, address);
                return GeocodeResult.Unlocatable($"Geocoding service returned {(int)response.StatusCode}.");
            }

            var parsed = await response.Content.ReadFromJsonAsync<GeocodingResponse>(cancellationToken: ct);
            return GeocodeGate.Evaluate(parsed);
        }
        // Expected external failures (network, token acquisition, malformed response body) degrade
        // to Unlocatable; cancellation and unexpected exceptions propagate so real bugs aren't masked.
        catch (HttpRequestException ex)
        {
            return LogAndFail(ex);
        }
        catch (AuthenticationFailedException ex)
        {
            return LogAndFail(ex);
        }
        catch (JsonException ex)
        {
            return LogAndFail(ex);
        }

        GeocodeResult LogAndFail(Exception ex)
        {
            logger.LogError(ex, "Geocoding request failed for {Address}.", address);
            return GeocodeResult.Unlocatable("Geocoding request failed.");
        }
    }
}
