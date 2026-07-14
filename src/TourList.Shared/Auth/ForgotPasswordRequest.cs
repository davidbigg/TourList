using System.ComponentModel.DataAnnotations;

namespace TourList.Shared.Auth;

/// <summary>Request a password-reset email (ACS Email; delivered in M5).</summary>
public sealed class ForgotPasswordRequest
{
    [Required]
    [EmailAddress]
    public required string Email { get; set; }
}
