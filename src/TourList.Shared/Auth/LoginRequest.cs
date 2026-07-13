using System.ComponentModel.DataAnnotations;

namespace TourList.Shared.Auth;

/// <summary>Login payload. Email is the login identifier.</summary>
public sealed class LoginRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}
