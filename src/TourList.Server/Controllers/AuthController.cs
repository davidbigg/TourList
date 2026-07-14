using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using TourList.Server.Data;
using TourList.Server.Options;
using TourList.Server.Services.Email;
using TourList.Shared;
using TourList.Shared.Auth;

namespace TourList.Server.Controllers;

/// <summary>
/// Cookie-based (BFF) auth. Sign-in sets an httpOnly+Secure+SameSite=Strict application cookie via
/// <see cref="SignInManager{T}"/> — the token never touches JS. Session revocation rides Identity's
/// security stamp (5-min validation interval); there is intentionally no refresh-token table.
/// </summary>
[ApiController]
[Route("api/auth")]
[EnableRateLimiting(RateLimitPolicies.Auth)]
public sealed class AuthController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IPasswordHasher<ApplicationUser> passwordHasher,
    IEmailDispatcher emailSender,
    IOptions<AcsEmailOptions> emailOptions,
    IHostEnvironment environment,
    ILogger<AuthController> logger) : ControllerBase
{
    // A password hash computed once and reused to spend equivalent CPU on the login path when no real
    // password verification would otherwise occur (unknown / inactive account) — see Login.
    private const string DummyPassword = "timing-equalization-placeholder";
    private static string? _dummyPasswordHash;

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthUserDto>> Register(RegisterRequest request)
    {
        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            IsActive = true,
        };

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            return IdentityValidationProblem(result);
        }

        await userManager.AddToRoleAsync(user, RoleNames.User);
        await signInManager.SignInAsync(user, isPersistent: true);
        return Ok(await BuildMeAsync(user));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthUserDto>> Login(LoginRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);

        // Uniform response for unknown user / inactive / wrong password (avoids user enumeration).
        if (user is null || !user.IsActive)
        {
            // Constant-time guard: this branch does no password hashing, so without matching work it
            // returns far faster than the wrong-password branch below — letting an attacker enumerate
            // accounts by response time. Verify the supplied password against a cached dummy hash
            // (computed once) and discard the result so the CPU cost mirrors a real verification.
            _dummyPasswordHash ??= passwordHasher.HashPassword(new ApplicationUser(), DummyPassword);
            passwordHasher.VerifyHashedPassword(new ApplicationUser(), _dummyPasswordHash, request.Password);

            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid email or password.");
        }

        var result = await signInManager.PasswordSignInAsync(
            user, request.Password, isPersistent: true, lockoutOnFailure: true);

        if (!result.Succeeded)
        {
            var title = result.IsLockedOut ? "Account temporarily locked." : "Invalid email or password.";
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: title);
        }

        return Ok(await BuildMeAsync(user));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is not null)
        {
            // Bump the security stamp so any outstanding cookies are rejected on next validation.
            await userManager.UpdateSecurityStampAsync(user);
        }

        await signInManager.SignOutAsync();
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<AuthUserDto>> Me()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null || !user.IsActive)
        {
            return Unauthorized();
        }

        return Ok(await BuildMeAsync(user));
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is not null && user.IsActive)
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var baseUrl = ResolvePublicBaseUrl();
            var resetLink =
                $"{baseUrl}/reset-password" +
                $"?email={Uri.EscapeDataString(user.Email!)}&token={Uri.EscapeDataString(token)}";

            await emailSender.SendPasswordResetAsync(user.Email!, resetLink);
        }

        // Always 200 — never reveal whether the account exists.
        return Ok();
    }

    /// <summary>
    /// Resolves the app's public origin for the password-reset link. Prefers the configured
    /// <see cref="AcsEmailOptions.BaseUrl"/> (authoritative); only falls back to the request origin in
    /// local development. The Host header is attacker-controlled and must never be trusted to build a
    /// security-sensitive link (host-header injection → reset-link poisoning / account takeover).
    /// </summary>
    private string ResolvePublicBaseUrl()
    {
        var configured = emailOptions.Value.BaseUrl?.Trim().TrimEnd('/');
        if (!string.IsNullOrEmpty(configured))
        {
            return configured;
        }

        if (!environment.IsDevelopment())
        {
            logger.LogWarning(
                "AcsEmail:BaseUrl is not configured; falling back to the request Host header to build the " +
                "password-reset link. Configure AcsEmail:BaseUrl so reset links don't depend on an untrusted header.");
        }

        return $"{Request.Scheme}://{Request.Host}";
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid or expired reset request.");
        }

        // ResetPasswordAsync rotates the security stamp on success → outstanding cookies are invalidated.
        var result = await userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
        return result.Succeeded ? Ok() : IdentityValidationProblem(result);
    }

    private async Task<AuthUserDto> BuildMeAsync(ApplicationUser user)
    {
        var roles = await userManager.GetRolesAsync(user);
        return new AuthUserDto { Email = user.Email!, Roles = [.. roles] };
    }

    private ActionResult IdentityValidationProblem(IdentityResult result)
    {
        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(error.Code, error.Description);
        }

        return ValidationProblem(ModelState);
    }
}
