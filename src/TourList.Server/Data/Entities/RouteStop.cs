namespace TourList.Server.Data.Entities;

/// <summary>
/// The load-bearing snapshot. A stop copies its source entry's address/coords/notes at add-time
/// into plain denormalized columns — <b>never a live join</b>. <see cref="SourceEntryId"/> is
/// provenance only (SetNull on entry delete), so a route stays intact and drivable when the
/// underlying entry or list is edited or deleted (acceptance scenario 11).
/// </summary>
public class RouteStop
{
    public Guid Id { get; set; }

    public Guid RouteId { get; set; }

    /// <summary>Zero-based position within the route's ordered stop list.</summary>
    public int Order { get; set; }

    /// <summary>Provenance link to the originating entry; null once that entry is deleted.</summary>
    public Guid? SourceEntryId { get; set; }

    public string SnapshotAddress { get; set; } = string.Empty;

    public double SnapshotLatitude { get; set; }

    public double SnapshotLongitude { get; set; }

    public string? SnapshotNotes { get; set; }

    public DateTime AddedUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Device-local "visited" state in v1 (not synced); persisted here for future sync.</summary>
    public bool Visited { get; set; }

    public SavedRoute? Route { get; set; }
}
