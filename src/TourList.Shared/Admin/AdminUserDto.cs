namespace TourList.Shared.Admin;

/// <summary>A user row in the admin user-management screen.</summary>
public sealed class AdminUserDto
{
    public Guid Id { get; init; }
    public required string Email { get; init; }
    public bool IsAdmin { get; init; }
    public bool IsActive { get; init; }
    public int RouteCount { get; init; }
    public DateTime CreatedUtc { get; init; }
}
