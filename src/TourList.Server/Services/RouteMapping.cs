using TourList.Server.Data.Entities;
using TourList.Shared.Routes;

namespace TourList.Server.Services;

/// <summary>Builds RouteStop entities from a DTO. Snapshots are copied verbatim; a
/// <c>SourceEntryId</c> that no longer exists is nulled (provenance only — the snapshot stands alone,
/// and this avoids an FK violation when the source entry has been deleted).</summary>
public static class RouteMapping
{
    public static List<RouteStop> BuildStops(RouteDto dto, ISet<Guid> existingEntryIds) =>
        dto.Stops.OrderBy(s => s.Order).Select((s, index) => new RouteStop
        {
            Id = s.Id == Guid.Empty ? Guid.NewGuid() : s.Id,
            Order = index,
            SourceEntryId = s.SourceEntryId is { } id && existingEntryIds.Contains(id) ? id : null,
            SnapshotAddress = s.SnapshotAddress,
            SnapshotLatitude = s.SnapshotLatitude,
            SnapshotLongitude = s.SnapshotLongitude,
            SnapshotNotes = s.SnapshotNotes,
            Visited = s.Visited,
        }).ToList();

    /// <summary>All source-entry ids referenced by the given routes (for a single existence query).</summary>
    public static List<Guid> ReferencedEntryIds(IEnumerable<RouteDto> routes) =>
        routes.SelectMany(r => r.Stops)
            .Where(s => s.SourceEntryId.HasValue)
            .Select(s => s.SourceEntryId!.Value)
            .Distinct()
            .ToList();
}
