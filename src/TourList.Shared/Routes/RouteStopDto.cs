namespace TourList.Shared.Routes;

/// <summary>
/// A stop in a route — a point-in-time <b>snapshot</b> of the source entry's address/coords/notes.
/// Never re-derived from the live entry, so routes stay intact when entries change or are deleted.
/// The snapshot fields are <c>init</c>-only (immutable once captured); <see cref="Order"/> and
/// <see cref="Visited"/> are intentionally mutable client-side working state (see remarks).
/// </summary>
public sealed class RouteStopDto
{
    public Guid Id { get; init; }

    /// <summary>
    /// Zero-based position in the ordered route. Mutable: the route builder reindexes it in place
    /// as the driver adds / removes / reorders stops.
    /// </summary>
    public int Order { get; set; }

    /// <summary>Provenance link to the originating entry (may be null once that entry is deleted).</summary>
    public Guid? SourceEntryId { get; init; }

    public required string SnapshotAddress { get; init; }
    public double SnapshotLatitude { get; init; }
    public double SnapshotLongitude { get; init; }
    public string? SnapshotNotes { get; init; }

    /// <summary>
    /// Device-local "visited" state (v1 not synced). Mutable: the Driving View toggles it in place
    /// as stops are checked off.
    /// </summary>
    public bool Visited { get; set; }
}
