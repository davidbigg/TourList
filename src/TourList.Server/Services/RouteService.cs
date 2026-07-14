using Microsoft.Data.SqlClient;
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

        // Best-effort (OwnerUserId, Name) uniqueness check. This is racy (TOCTOU): a concurrent upsert
        // can take the same name between here and SaveChanges. The filtered unique index is the real
        // backstop — see the DbUpdateException translation below.
        var nameTaken = await db.Routes.AnyAsync(
            r => r.OwnerUserId == userId && r.Id != effectiveId && r.Name == dto.Name, ct);
        if (nameTaken)
        {
            return new UpsertResult(UpsertStatus.NameConflict, null);
        }

        var existingEntryIds = await ExistingEntryIdsAsync([dto], ct);
        var now = DateTime.UtcNow;

        SavedRoute route;
        if (existing is null)
        {
            route = new SavedRoute
            {
                Id = effectiveId == Guid.Empty ? Guid.NewGuid() : effectiveId,
                OwnerUserId = userId,
                CreatedUtc = now,
            };
            ApplyDto(route, dto, existingEntryIds, now);
            db.Routes.Add(route);
        }
        else
        {
            route = existing;
            db.RouteStops.RemoveRange(existing.Stops);
            ApplyDto(existing, dto, existingEntryIds, now);
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsNameUniquenessViolation(ex))
        {
            // Lost the TOCTOU race: a concurrent writer committed the same (OwnerUserId, Name) after
            // our AnyAsync check. Surface the intended 409 rather than a generic 500.
            return new UpsertResult(UpsertStatus.NameConflict, null);
        }

        return new UpsertResult(UpsertStatus.Ok, route.ToDto());
    }

    // SQL Server raises 2601 (duplicate key row in a unique index) / 2627 (unique constraint
    // violation) when the filtered (OwnerUserId, Name) index rejects a concurrent duplicate. That
    // index is the only one a normal upsert contends on (PKs are unique Guids), so treat it as a
    // name conflict.
    private static bool IsNameUniquenessViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: 2601 or 2627 };

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
