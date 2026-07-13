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
}
