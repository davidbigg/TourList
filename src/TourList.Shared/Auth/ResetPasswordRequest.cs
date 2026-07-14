using System.ComponentModel.DataAnnotations;

namespace TourList.Shared.Auth;

/// <summary>Complete a password reset using the token from the emailed reset link.</summary>
public sealed class ResetPasswordRequest
{
    [Required]
    [EmailAddress]
    public required string Email { get; set; }

    [Required]
    public required string Token { get; set; }

    [Required]
    [StringLength(128, MinimumLength = 8)]
    public required string NewPassword { get; set; }
}
