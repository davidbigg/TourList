using Microsoft.EntityFrameworkCore;
using TourList.Server.Data;
using TourList.Server.Data.Entities;
using TourList.Server.Mapping;
using TourList.Shared.Routes;

namespace TourList.Server.Services;

public enum UpsertStatus { Ok, Forbidden, NameConflict }

public sealed record UpsertResult(UpsertStatus Status, RouteDto? Route);

/// <summary>Owner-scoped route CRUD for registered users. Not-owned rows are invisible (404).</summary>
public sealed class RouteService(AppDbContext db)
{
    public async Task<List<RouteDto>> GetAllForUserAsync(Guid userId, CancellationToken ct)
    {
        var routes = await db.Routes
            .Include(r => r.Stops)
            .Where(r => r.OwnerUserId == userId)
            .OrderByDescending(r => r.UpdatedUtc)
            .ToListAsync(ct);

        return routes.Select(r => r.ToDto()).ToList();
    }

    public async Task<RouteDto?> GetForUserAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var route = await db.Routes
            .Include(r => r.Stops)
            .FirstOrDefaultAsync(r => r.Id == id && r.OwnerUserId == userId, ct);

        return route?.ToDto();
    }

    public async Task<UpsertResult> UpsertAsync(Guid userId, RouteDto dto, Guid? routeId, CancellationToken ct)
    {
        // For PUT the URL id is authoritative; for POST fall back to the body id. Either way the
        // (immutable) DTO is never mutated — the resolved id flows through this local only.
        var effectiveId = routeId ?? dto.Id;
        var existing = await db.Routes.Include(r => r.Stops).FirstOrDefaultAsync(r => r.Id == effectiveId, ct);
        if (existing is not null && existing.OwnerUserId != userId)
        {
            return new UpsertResult(UpsertStatus.Forbidden, null);
        }

        // (OwnerUserId, Name) uniqueness — enforced here (a filtered unique index is the DB backstop).
        var nameTaken = await db.Routes.AnyAsync(
            r => r.OwnerUserId == userId && r.Id != effectiveId && r.Name == dto.Name, ct);
        if (nameTaken)
        {
            return new UpsertResult(UpsertStatus.NameConflict, null);
        }

        var existingEntryIds = await ExistingEntryIdsAsync([dto], ct);
        var now = DateTime.UtcNow;

        if (existing is null)
        {
            var route = new SavedRoute
            {
                Id = effectiveId == Guid.Empty ? Guid.NewGuid() : effectiveId,
                OwnerUserId = userId,
                CreatedUtc = now,
            };
            ApplyDto(route, dto, existingEntryIds, now);
            db.Routes.Add(route);
            await db.SaveChangesAsync(ct);
            return new UpsertResult(UpsertStatus.Ok, route.ToDto());
        }

        db.RouteStops.RemoveRange(existing.Stops);
        ApplyDto(existing, dto, existingEntryIds, now);
        await db.SaveChangesAsync(ct);
        return new UpsertResult(UpsertStatus.Ok, existing.ToDto());
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var route = await db.Routes.FirstOrDefaultAsync(r => r.Id == id && r.OwnerUserId == userId, ct);
        if (route is null)
        {
            return false;
        }

        db.Routes.Remove(route); // cascades to stops
        await db.SaveChangesAsync(ct);
        return true;
    }

    internal async Task<HashSet<Guid>> ExistingEntryIdsAsync(IEnumerable<RouteDto> routes, CancellationToken ct)
    {
        var ids = RouteMapping.ReferencedEntryIds(routes);
        if (ids.Count == 0)
        {
            return [];
        }

        var found = await db.Entries.Where(e => ids.Contains(e.Id)).Select(e => e.Id).ToListAsync(ct);
        return found.ToHashSet();
    }

    private static void ApplyDto(SavedRoute route, RouteDto dto, ISet<Guid> existingEntryIds, DateTime now)
    {
        route.Name = dto.Name;
        route.StartPointType = dto.StartPointType;
        route.StartStopId = dto.StartStopId;
        route.StartLatitude = dto.StartLatitude;
        route.StartLongitude = dto.StartLongitude;
        route.StartLabel = dto.StartLabel;
        route.UpdatedUtc = now;
        route.Stops = RouteMapping.BuildStops(dto, existingEntryIds);
    }
}
