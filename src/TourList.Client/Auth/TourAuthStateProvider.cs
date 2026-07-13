using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using TourList.Shared;
using TourList.Shared.Auth;

namespace TourList.Client.Auth;

/// <summary>
/// Hydrates auth state from <c>GET /api/auth/me</c> (the httpOnly cookie rides the request). The
/// result is cached until <see cref="NotifyChanged"/> invalidates it, so route storage can pick
/// guest vs. API without a network call per operation.
/// </summary>
public sealed class TourAuthStateProvider(AuthApi auth) : AuthenticationStateProvider
{
    private Task<AuthenticationState>? _cache;

    public override Task<AuthenticationState> GetAuthenticationStateAsync() => _cache ??= LoadAsync();

    public void NotifyChanged()
    {
        _cache = null;
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    private async Task<AuthenticationState> LoadAsync()
    {
        var user = await auth.GetMeAsync();
        return new AuthenticationState(BuildPrincipal(user));
    }

    private static ClaimsPrincipal BuildPrincipal(AuthUserDto? user)
    {
        if (user is null)
        {
            return new ClaimsPrincipal(new ClaimsIdentity());
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, user.Email),
            new(ClaimTypes.Email, user.Email),
        };
        claims.AddRange(user.Roles.Select(r => new Claim(ClaimTypes.Role, r)));

        // Authentication type is non-empty → IsAuthenticated is true.
        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "TourList"));
    }
}
