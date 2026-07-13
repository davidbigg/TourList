namespace TourList.Shared.Admin;

/// <summary>A user row in the admin user-management screen.</summary>
public sealed class AdminUserDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }
    public bool IsActive { get; set; }
    public int RouteCount { get; set; }
    public DateTime CreatedUtc { get; set; }
}
