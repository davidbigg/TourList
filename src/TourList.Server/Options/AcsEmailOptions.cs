namespace TourList.Server.Options;

/// <summary>
/// Azure Communication Services Email config (section <c>AcsEmail</c>). Managed identity via
/// <c>Endpoint</c> is preferred; a <c>ConnectionString</c> is an optional fallback. When unset,
/// password-reset links are logged instead of emailed (dev / not-configured).
/// </summary>
public sealed class AcsEmailOptions
{
    public const string SectionName = "AcsEmail";

    public string? Endpoint { get; set; }
    public string? SenderAddress { get; set; }
    public string? ConnectionString { get; set; }

    /// <summary>
    /// The application's public origin (e.g. <c>https://app.example.com</c>) used to build absolute
    /// links in emails (password reset). Set from config / Key Vault in real deployments so links never
    /// depend on the attacker-controllable Host header. When unset, links fall back to the request
    /// origin — intended for local development only.
    /// </summary>
    public string? BaseUrl { get; set; }
}
