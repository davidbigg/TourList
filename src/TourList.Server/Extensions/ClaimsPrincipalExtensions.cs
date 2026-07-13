using System.Security.Claims;

namespace TourList.Server.Extensions;

public static class ClaimsPrincipalExtensions
{
    /// <summary>The Identity user id (from the NameIdentifier claim), or null if unauthenticated/malformed.</summary>
    public static Guid? GetUserId(this ClaimsPrincipal user)
    {
        var id = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(id, out var guid) ? guid : null;
    }
}
