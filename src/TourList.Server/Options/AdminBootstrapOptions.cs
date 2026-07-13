using System.ComponentModel.DataAnnotations;

namespace TourList.Server.Options;

/// <summary>
/// Initial-admin bootstrap credentials (config section <c>AdminBootstrap</c>). Sourced from
/// user-secrets locally and Key Vault in Azure. Used by <c>DbSeeder</c> only when zero admins exist.
/// </summary>
public sealed class AdminBootstrapOptions
{
    public const string SectionName = "AdminBootstrap";

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(8)]
    public string Password { get; set; } = string.Empty;
}
