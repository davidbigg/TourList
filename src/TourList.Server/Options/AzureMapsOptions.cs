namespace TourList.Server.Options;

/// <summary>
/// Azure Maps configuration (section <c>AzureMaps</c>). When <see cref="Enabled"/> is false
/// (default), geocoding is skipped and entries stay <c>Pending</c> — the service is fully wired
/// and lights up once credentials are supplied.
/// </summary>
public sealed class AzureMapsOptions
{
    public const string SectionName = "AzureMaps";

    /// <summary>Master switch. False until an Azure Maps account is configured.</summary>
    public bool Enabled { get; set; }

    /// <summary>Base URL for the Get Geocoding REST API.</summary>
    public string GeocodingEndpoint { get; set; } = "https://atlas.microsoft.com/";

    /// <summary>Get Geocoding API version (see implementation plan).</summary>
    public string ApiVersion { get; set; } = "2026-01-01";

    /// <summary>Azure Maps account client id, sent as <c>x-ms-client-id</c> with AAD tokens.</summary>
    public string? ClientId { get; set; }

    /// <summary>Local-dev fallback: shared subscription key (prefer managed identity in Azure).</summary>
    public string? SubscriptionKey { get; set; }

    /// <summary>TTL for tokens minted by the guest-reachable maps token endpoint (M2).</summary>
    public int TokenTtlSeconds { get; set; } = 600;
}
