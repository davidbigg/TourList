using TourList.Shared.Enums;

namespace TourList.Server.Data.Entities;

/// <summary>
/// A user-assembled, named, ordered sequence of stops (the spec's "Route"; named <c>SavedRoute</c>
/// in code to avoid colliding with <see cref="Microsoft.AspNetCore.Routing.Route"/>). Owned by a
/// registered user (guest routes live only on-device in IndexedDB). Deleting a route cascades to its
/// stops; removing the owner cascades to the route.
/// </summary>
public class SavedRoute
{
    public Guid Id { get; set; }

    public Guid OwnerUserId { get; set; }

    public string Name { get; set; } = string.Empty;

    public StartPointType StartPointType { get; set; } = StartPointType.CurrentLocation;

    /// <summary>When <see cref="StartPointType"/> is <c>Stop</c>, the stop used as the origin.</summary>
    public Guid? StartStopId { get; set; }

    /// <summary>When <see cref="StartPointType"/> is <c>Manual</c>, the chosen origin coordinate + label.</summary>
    public double? StartLatitude { get; set; }

    public double? StartLongitude { get; set; }

    public string? StartLabel { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;

    public ICollection<RouteStop> Stops { get; set; } = new List<RouteStop>();
}
