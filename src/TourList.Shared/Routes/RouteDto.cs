using TourList.Shared.Enums;

namespace TourList.Shared.Routes;

/// <summary>
/// A user-assembled, named, ordered route (the full aggregate). Serves as both the IndexedDB
/// record for guest routes and the API shape for registered users' synced routes.
/// </summary>
public sealed class RouteDto
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    public StartPointType StartPointType { get; set; } = StartPointType.CurrentLocation;

    /// <summary>When <see cref="StartPointType"/> is Stop, the id of the stop used as the origin.</summary>
    public Guid? StartStopId { get; set; }

    /// <summary>When <see cref="StartPointType"/> is Manual, the chosen origin coordinate + label.</summary>
    public double? StartLatitude { get; set; }
    public double? StartLongitude { get; set; }
    public string? StartLabel { get; set; }

    public List<RouteStopDto> Stops { get; set; } = [];

    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}
