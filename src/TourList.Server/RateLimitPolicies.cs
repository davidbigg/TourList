namespace TourList.Server;

/// <summary>Named per-IP rate-limit policies (see Program.cs for limits).</summary>
public static class RateLimitPolicies
{
    /// <summary>Protects the guest-reachable, cost-sensitive Azure Maps token endpoint.</summary>
    public const string MapsToken = "maps-token";

    /// <summary>Throttles auth endpoints (login/register/password) against brute force.</summary>
    public const string Auth = "auth";
}
