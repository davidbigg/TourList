namespace TourList.Shared.Maps;

/// <summary>
/// Short-lived Azure Maps access token minted server-side (managed identity) for the anonymous
/// Web SDK auth flow, plus the Azure Maps account client id.
/// </summary>
public sealed class MapsTokenResponse
{
    public string Token { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public DateTime ExpiresOnUtc { get; set; }
}
