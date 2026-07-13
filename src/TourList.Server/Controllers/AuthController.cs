using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TourList.Server.Data;
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
public sealed class AuthController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    ILogger<AuthController> logger) : ControllerBase
{
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

            // TODO (M5): deliver the reset link via Azure Communication Services Email.
            // Until then, surface the token in logs for local testing only.
            logger.LogInformation("Password reset token generated for {Email}: {Token}", request.Email, token);
        }

        // Always 200 — never reveal whether the account exists.
        return Ok();
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
