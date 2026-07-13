namespace TourList.Shared.Routes;

/// <summary>
/// A stop in a route — a point-in-time <b>snapshot</b> of the source entry's address/coords/notes.
/// Never re-derived from the live entry, so routes stay intact when entries change or are deleted.
/// </summary>
public sealed class RouteStopDto
{
    public Guid Id { get; set; }

    /// <summary>Zero-based position in the ordered route.</summary>
    public int Order { get; set; }

    /// <summary>Provenance link to the originating entry (may be null once that entry is deleted).</summary>
    public Guid? SourceEntryId { get; set; }

    public string SnapshotAddress { get; set; } = string.Empty;
    public double SnapshotLatitude { get; set; }
    public double SnapshotLongitude { get; set; }
    public string? SnapshotNotes { get; set; }

    /// <summary>Device-local "visited" state (v1 not synced).</summary>
    public bool Visited { get; set; }
}
