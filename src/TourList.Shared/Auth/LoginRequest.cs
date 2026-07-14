using System.ComponentModel.DataAnnotations;

namespace TourList.Shared.Auth;

/// <summary>Login payload. Email is the login identifier.</summary>
public sealed class LoginRequest
{
    [Required]
    [EmailAddress]
    public required string Email { get; set; }

    [Required]
    public required string Password { get; set; }
}
