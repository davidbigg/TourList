namespace TourList.Shared.Auth;

/// <summary>
/// Current authenticated identity returned by <c>GET /api/auth/me</c>.
/// Cached client-side for optimistic role-gated render, then reconfirmed.
/// </summary>
public sealed class AuthUserDto
{
    public string Email { get; set; } = string.Empty;

    public IReadOnlyList<string> Roles { get; set; } = [];

    public bool IsAdmin => Roles.Contains(RoleNames.Admin);
}
