using TourList.Shared.Entries;
using TourList.Shared.Enums;
using TourList.Shared.Routes;

namespace TourList.Client.Services;

/// <summary>
/// The in-progress route shared across pages: the map's "add to route" action appends snapshot
/// stops here; the builder page arranges/names/saves them. Stops capture the entry's
/// address/coords/notes at add-time (never a live reference).
/// </summary>
public sealed class RouteBuilderState
{
    private readonly List<RouteStopDto> _stops = [];

    public Guid? EditingRouteId { get; private set; }
    public string Name { get; set; } = string.Empty;
    public StartPointType StartPointType { get; set; } = StartPointType.CurrentLocation;
    public Guid? StartStopId { get; set; }
    public double? StartLatitude { get; set; }
    public double? StartLongitude { get; set; }
    public string? StartLabel { get; set; }

    public IReadOnlyList<RouteStopDto> Stops => _stops;
    public int Count => _stops.Count;

    /// <summary>Raised whenever the working route changes so subscribed components re-render.</summary>
    public event Action? Changed;

    public bool Contains(Guid entryId) => _stops.Any(s => s.SourceEntryId == entryId);

    /// <summary>Snapshots a located entry as a new stop. No-op for entries without coordinates or duplicates.</summary>
    public bool AddEntry(EntryDto entry)
    {
        if (entry.Latitude is not { } lat || entry.Longitude is not { } lng)
        {
            return false;
        }

        if (Contains(entry.Id))
        {
            return false;
        }

        _stops.Add(new RouteStopDto
        {
            Id = Guid.NewGuid(),
            SourceEntryId = entry.Id,
            SnapshotAddress = string.IsNullOrWhiteSpace(entry.FormattedAddress) ? entry.RawAddress : entry.FormattedAddress!,
            SnapshotLatitude = lat,
            SnapshotLongitude = lng,
            SnapshotNotes = entry.Notes,
        });

        Reindex();
        return true;
    }

    public void Remove(Guid stopId)
    {
        var removed = _stops.RemoveAll(s => s.Id == stopId) > 0;
        if (StartStopId == stopId)
        {
            StartPointType = StartPointType.CurrentLocation;
            StartStopId = null;
        }
        if (removed)
        {
            Reindex();
        }
    }

    public void Move(Guid stopId, int direction)
    {
        var index = _stops.FindIndex(s => s.Id == stopId);
        var target = index + direction;
        if (index < 0 || target < 0 || target >= _stops.Count)
        {
            return;
        }

        (_stops[index], _stops[target]) = (_stops[target], _stops[index]);
        Reindex();
    }

    public void ReorderByIndex(int oldIndex, int newIndex)
    {
        if (oldIndex < 0 || oldIndex >= _stops.Count || newIndex < 0 || newIndex >= _stops.Count)
        {
            return;
        }

        var stop = _stops[oldIndex];
        _stops.RemoveAt(oldIndex);
        _stops.Insert(newIndex, stop);
        Reindex();
    }

    public void Clear()
    {
        _stops.Clear();
        EditingRouteId = null;
        Name = string.Empty;
        StartPointType = StartPointType.CurrentLocation;
        StartStopId = null;
        StartLatitude = StartLongitude = null;
        StartLabel = null;
        Changed?.Invoke();
    }

    public void LoadForEdit(RouteDto route)
    {
        _stops.Clear();
        _stops.AddRange(route.Stops.OrderBy(s => s.Order));
        EditingRouteId = route.Id;
        Name = route.Name;
        StartPointType = route.StartPointType;
        StartStopId = route.StartStopId;
        StartLatitude = route.StartLatitude;
        StartLongitude = route.StartLongitude;
        StartLabel = route.StartLabel;
        Reindex();
    }

    public RouteDto BuildRoute()
    {
        var now = DateTime.UtcNow;
        return new RouteDto
        {
            Id = EditingRouteId ?? Guid.NewGuid(),
            Name = string.IsNullOrWhiteSpace(Name) ? "Untitled route" : Name.Trim(),
            StartPointType = StartPointType,
            StartStopId = StartPointType == StartPointType.Stop ? StartStopId : null,
            StartLatitude = StartPointType == StartPointType.Manual ? StartLatitude : null,
            StartLongitude = StartPointType == StartPointType.Manual ? StartLongitude : null,
            StartLabel = StartPointType == StartPointType.Manual ? StartLabel : null,
            Stops = _stops.Select((s, i) => new RouteStopDto
            {
                Id = s.Id,
                Order = i,
                SourceEntryId = s.SourceEntryId,
                SnapshotAddress = s.SnapshotAddress,
                SnapshotLatitude = s.SnapshotLatitude,
                SnapshotLongitude = s.SnapshotLongitude,
                SnapshotNotes = s.SnapshotNotes,
                Visited = s.Visited,
            }).ToList(),
            CreatedUtc = now,
            UpdatedUtc = now,
        };
    }

    private void Reindex()
    {
        for (var i = 0; i < _stops.Count; i++)
        {
            _stops[i].Order = i;
        }
        Changed?.Invoke();
    }
}
