namespace TourList.Shared.Enums;

/// <summary>
/// Lifecycle state of an <c>Entry</c>'s geocoded location.
/// </summary>
public enum GeocodeStatus
{
    /// <summary>Address has not yet been geocoded.</summary>
    Pending = 0,

    /// <summary>Address resolved to coordinates that passed the confidence gate.</summary>
    Located = 1,

    /// <summary>Address could not be resolved with sufficient confidence; flagged for admin resolution.</summary>
    Unlocatable = 2,
}
