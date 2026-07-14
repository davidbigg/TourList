namespace TourList.Shared.Auth;

/// <summary>
/// Current authenticated identity returned by <c>GET /api/auth/me</c>.
/// Cached client-side for optimistic role-gated render, then reconfirmed.
/// </summary>
public sealed class AuthUserDto
{
    public required string Email { get; init; }

    public IReadOnlyList<string> Roles { get; init; } = [];

    public bool IsAdmin => Roles.Contains(RoleNames.Admin);
}
