using TourList.Server.Data.Entities;
using TourList.Shared.Entries;
using TourList.Shared.Lists;
using TourList.Shared.Routes;

namespace TourList.Server.Mapping;

/// <summary>Entity → DTO projections (keeps EF types from leaking past the API boundary).</summary>
public static class DtoMappers
{
    public static ListDto ToDto(this AddressList list, int entryCount) => new()
    {
        Id = list.Id,
        Name = list.Name,
        EntryCount = entryCount,
        CreatedUtc = list.CreatedUtc,
        UpdatedUtc = list.UpdatedUtc,
    };

    public static EntryDto ToDto(this Entry entry) => new()
    {
        Id = entry.Id,
        ListId = entry.ListId,
        RawAddress = entry.RawAddress,
        FormattedAddress = entry.FormattedAddress,
        Notes = entry.Notes,
        Latitude = entry.Latitude,
        Longitude = entry.Longitude,
        GeocodeStatus = entry.GeocodeStatus,
        GeocodeConfidence = entry.GeocodeConfidence,
        ManuallyLocated = entry.ManuallyLocated,
        LastGeocodedUtc = entry.LastGeocodedUtc,
    };

    public static RouteDto ToDto(this SavedRoute route) => new()
    {
        Id = route.Id,
        Name = route.Name,
        StartPointType = route.StartPointType,
        StartStopId = route.StartStopId,
        StartLatitude = route.StartLatitude,
        StartLongitude = route.StartLongitude,
        StartLabel = route.StartLabel,
        CreatedUtc = route.CreatedUtc,
        UpdatedUtc = route.UpdatedUtc,
        Stops = route.Stops.OrderBy(s => s.Order).Select(s => new RouteStopDto
        {
            Id = s.Id,
            Order = s.Order,
            SourceEntryId = s.SourceEntryId,
            SnapshotAddress = s.SnapshotAddress,
            SnapshotLatitude = s.SnapshotLatitude,
            SnapshotLongitude = s.SnapshotLongitude,
            SnapshotNotes = s.SnapshotNotes,
            Visited = s.Visited,
        }).ToList(),
    };
}
