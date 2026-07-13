using System.ComponentModel.DataAnnotations;

namespace TourList.Shared.Auth;

/// <summary>Self-registration payload. Email is the login identifier.</summary>
public sealed class RegisterRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(128, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;
}
