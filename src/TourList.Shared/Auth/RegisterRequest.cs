using System.ComponentModel.DataAnnotations;

namespace TourList.Shared.Auth;

/// <summary>Self-registration payload. Email is the login identifier.</summary>
public sealed class RegisterRequest
{
    [Required]
    [EmailAddress]
    public required string Email { get; set; }

    [Required]
    [StringLength(128, MinimumLength = 8)]
    public required string Password { get; set; }
}
