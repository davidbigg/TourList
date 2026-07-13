using System.ComponentModel.DataAnnotations;

namespace TourList.Shared.Auth;

/// <summary>Complete a password reset using the token from the emailed reset link.</summary>
public sealed class ResetPasswordRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Token { get; set; } = string.Empty;

    [Required]
    [StringLength(128, MinimumLength = 8)]
    public string NewPassword { get; set; } = string.Empty;
}
