namespace TourList.Shared.Enums;

/// <summary>
/// How a route's start point is determined.
/// </summary>
public enum StartPointType
{
    /// <summary>Use the driver's live GPS position at drive time (default).</summary>
    CurrentLocation = 0,

    /// <summary>Start from one of the route's own stops.</summary>
    Stop = 1,

    /// <summary>Start from a manually chosen coordinate.</summary>
    Manual = 2,
}
