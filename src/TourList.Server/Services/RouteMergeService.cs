using System.Data;
using Microsoft.EntityFrameworkCore;
using TourList.Server.Data;
using TourList.Server.Data.Entities;
using TourList.Shared.Routes;

namespace TourList.Server.Services;

/// <summary>
/// One-time guest→account route migration (acceptance scenario 13). Idempotent (gated by
/// <c>GuestRoutesMigratedUtc</c>); dedupes by name-excluded content fingerprint; renames a new
/// route whose name collides with an existing one; overwrites nothing. Runs in a serializable
/// transaction (wrapped in the EF execution strategy to coexist with connection-retry).
/// </summary>
public sealed class RouteMergeService(AppDbContext db, RouteService routeService)
{
    public async Task<MergeRoutesResponse> MergeAsync(Guid userId, IReadOnlyList<RouteDto> guestRoutes, CancellationToken ct)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
                ?? throw new InvalidOperationException("User not found.");

            if (user.GuestRoutesMigratedUtc is not null)
            {
                // Already migrated on this account — no-op (idempotent).
                await tx.CommitAsync(ct);
                return new MergeRoutesResponse { AlreadyMigrated = true };
            }

            var existingRoutes = await db.Routes.Include(r => r.Stops)
                .Where(r => r.OwnerUserId == userId).ToListAsync(ct);

            var byFingerprint = new Dictionary<string, SavedRoute>();
            foreach (var r in existingRoutes)
            {
                byFingerprint.TryAdd(RouteFingerprint.Compute(r), r);
            }

            var takenNames = new HashSet<string>(existingRoutes.Select(r => r.Name), StringComparer.OrdinalIgnoreCase);
            var takenIds = existingRoutes.Select(r => r.Id).ToHashSet();
            var existingEntryIds = await routeService.ExistingEntryIdsAsync(guestRoutes, ct);

            var results = new List<MergeRouteResult>();
            var createdCount = 0;
            var dedupedCount = 0;
            var renamedCount = 0;
            var now = DateTime.UtcNow;

            foreach (var guest in guestRoutes)
            {
                var fingerprint = RouteFingerprint.Compute(guest);
                if (byFingerprint.TryGetValue(fingerprint, out var match))
                {
                    results.Add(new MergeRouteResult
                    {
                        ClientId = guest.Id,
                        Outcome = RouteMergeOutcome.DedupedExisting,
                        ServerRouteId = match.Id,
                        FinalName = match.Name,
                    });
                    dedupedCount++;
                    continue;
                }

                var finalName = MakeUniqueName(guest.Name, takenNames);
                var renamed = !string.Equals(finalName, guest.Name?.Trim(), StringComparison.Ordinal);
                var id = guest.Id != Guid.Empty && takenIds.Add(guest.Id) ? guest.Id : NewTrackedId(takenIds);

                var route = new SavedRoute
                {
                    Id = id,
                    OwnerUserId = userId,
                    Name = finalName,
                    StartPointType = guest.StartPointType,
                    StartStopId = guest.StartStopId,
                    StartLatitude = guest.StartLatitude,
                    StartLongitude = guest.StartLongitude,
                    StartLabel = guest.StartLabel,
                    CreatedUtc = now,
                    UpdatedUtc = now,
                    Stops = RouteMapping.BuildStops(guest, existingEntryIds),
                };
                db.Routes.Add(route);

                byFingerprint[fingerprint] = route;
                takenNames.Add(finalName);
                results.Add(new MergeRouteResult
                {
                    ClientId = guest.Id,
                    Outcome = renamed ? RouteMergeOutcome.RenamedCopy : RouteMergeOutcome.Created,
                    ServerRouteId = route.Id,
                    FinalName = finalName,
                });
                if (renamed)
                {
                    renamedCount++;
                }
                else
                {
                    createdCount++;
                }
            }

            user.GuestRoutesMigratedUtc = now;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return new MergeRoutesResponse
            {
                Results = results,
                Counts = new MergeCounts
                {
                    Created = createdCount,
                    Deduped = dedupedCount,
                    Renamed = renamedCount,
                },
            };
        });
    }

    private static string MakeUniqueName(string? name, ISet<string> taken)
    {
        var baseName = string.IsNullOrWhiteSpace(name) ? "Untitled route" : name.Trim();
        if (!taken.Contains(baseName))
        {
            return baseName;
        }

        var candidate = $"{baseName} (imported)";
        var n = 2;
        while (taken.Contains(candidate))
        {
            candidate = $"{baseName} (imported {n++})";
        }
        return candidate;
    }

    private static Guid NewTrackedId(HashSet<Guid> taken)
    {
        Guid id;
        do
        {
            id = Guid.NewGuid();
        }
        while (!taken.Add(id));
        return id;
    }
}
