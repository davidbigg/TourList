using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using TourList.Server.Data.Entities;
using TourList.Shared.Enums;
using TourList.Shared.Routes;

namespace TourList.Server.Services;

/// <summary>
/// Content fingerprint used to dedupe routes during the guest→account merge. Deliberately
/// <b>name-excluded</b>: the start point + the ordered stops (address, lat/lng at 6dp, notes) define
/// identity, so a renamed copy of the same stops is treated as a duplicate. Pure + unit-tested.
/// </summary>
public static class RouteFingerprint
{
    public static string Compute(RouteDto route)
    {
        var stops = route.Stops.OrderBy(s => s.Order).ToList();
        var startStopIndex = route.StartPointType == StartPointType.Stop && route.StartStopId is { } id
            ? NullIfNegative(stops.FindIndex(s => s.Id == id))
            : null;

        return Hash(
            route.StartPointType, route.StartLatitude, route.StartLongitude, route.StartLabel, startStopIndex,
            stops.Select(s => (s.SnapshotAddress, s.SnapshotLatitude, s.SnapshotLongitude, s.SnapshotNotes)));
    }

    public static string Compute(SavedRoute route)
    {
        var stops = route.Stops.OrderBy(s => s.Order).ToList();
        var startStopIndex = route.StartPointType == StartPointType.Stop && route.StartStopId is { } id
            ? NullIfNegative(stops.FindIndex(s => s.Id == id))
            : null;

        return Hash(
            route.StartPointType, route.StartLatitude, route.StartLongitude, route.StartLabel, startStopIndex,
            stops.Select(s => (s.SnapshotAddress, s.SnapshotLatitude, s.SnapshotLongitude, s.SnapshotNotes)));
    }

    private static int? NullIfNegative(int index) => index >= 0 ? index : null;

    private static string Hash(
        StartPointType startType,
        double? startLat,
        double? startLng,
        string? startLabel,
        int? startStopIndex,
        IEnumerable<(string Address, double Lat, double Lng, string? Notes)> stops)
    {
        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"start|{startType}|");
        switch (startType)
        {
            case StartPointType.Manual:
                sb.Append(CultureInfo.InvariantCulture, $"{startLat:F6},{startLng:F6},{startLabel}");
                break;
            case StartPointType.Stop:
                sb.Append(CultureInfo.InvariantCulture, $"idx{startStopIndex}");
                break;
        }

        sb.Append('\n');
        foreach (var s in stops)
        {
            sb.Append(CultureInfo.InvariantCulture, $"{s.Address}|{s.Lat:F6}|{s.Lng:F6}|{s.Notes}\n");
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())));
    }
}
