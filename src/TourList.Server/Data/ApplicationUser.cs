using Microsoft.AspNetCore.Identity;

namespace TourList.Server.Data;

/// <summary>
/// Application user. Email is the login identifier (see <c>Program</c> Identity options).
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    /// <summary>
    /// Deactivated users cannot log in and their outstanding cookies are rejected (security-stamp bump),
    /// but their synced routes are retained (reactivation restores access). Removal purges routes.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Set the first time this account absorbs a device's guest routes. Non-null ⇒ the one-time
    /// guest→account merge has already run; further merge attempts are no-ops.
    /// </summary>
    public DateTime? GuestRoutesMigratedUtc { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
