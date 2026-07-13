using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TourList.Server.Data;
using TourList.Server.Data.Entities;
using TourList.Server.Tests.Infrastructure;
using TourList.Shared.Enums;

namespace TourList.Server.Tests;

/// <summary>
/// Acceptance scenario 11: deleting an entry (or its whole list) must not touch a saved route —
/// the stop keeps its snapshot and stays drivable; only the provenance link is nulled.
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class RouteSnapshotSurvivalTests(SqlServerContainerFixture fixture)
{
    private ServiceProvider BuildServices() =>
        new ServiceCollection()
            .AddLogging()
            .AddDbContext<AppDbContext>(o => o.UseSqlServer(fixture.ConnectionStringFor("tl_snapshot_tests")))
            .AddIdentityCore<ApplicationUser>()
            .AddEntityFrameworkStores<AppDbContext>()
            .Services
            .BuildServiceProvider();

    [Fact]
    public async Task Route_stop_survives_deletion_of_its_source_entry_and_list()
    {
        await using var sp = BuildServices();
        using (var scope = sp.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
        }

        var users = sp.GetRequiredService<UserManager<ApplicationUser>>();
        var owner = new ApplicationUser { UserName = $"o-{Guid.NewGuid():N}@t.local", Email = "o@t.local", IsActive = true };
        await users.CreateAsync(owner, "Passw0rd!23");

        Guid listId, entryId, routeId, stopId;

        using (var scope = sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var list = new AddressList { Id = Guid.NewGuid(), Name = "Doomed list" };
            var entry = new Entry
            {
                Id = Guid.NewGuid(), ListId = list.Id, RawAddress = "500 Cedar St",
                Latitude = 47.6, Longitude = -122.3, GeocodeStatus = GeocodeStatus.Located,
            };
            var stop = new RouteStop
            {
                Id = Guid.NewGuid(), Order = 0, SourceEntryId = entry.Id,
                SnapshotAddress = "500 Cedar St", SnapshotLatitude = 47.6, SnapshotLongitude = -122.3,
                SnapshotNotes = "snapshot stands alone",
            };
            var route = new SavedRoute { Id = Guid.NewGuid(), OwnerUserId = owner.Id, Name = "Survivor", Stops = { stop } };

            db.Lists.Add(list);
            db.Entries.Add(entry);
            db.Routes.Add(route);
            await db.SaveChangesAsync();
            (listId, entryId, routeId, stopId) = (list.Id, entry.Id, route.Id, stop.Id);
        }

        // Delete the source entry — SetNull, not cascade.
        using (var scope = sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Entries.Remove(await db.Entries.FirstAsync(e => e.Id == entryId));
            await db.SaveChangesAsync();
        }

        using (var scope = sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var stop = await db.RouteStops.FirstOrDefaultAsync(s => s.Id == stopId);
            Assert.NotNull(stop);
            Assert.Null(stop!.SourceEntryId);                       // provenance nulled
            Assert.Equal("500 Cedar St", stop.SnapshotAddress);     // snapshot intact
            Assert.Equal("snapshot stands alone", stop.SnapshotNotes);
        }

        // Delete the whole list (cascades to its entries only).
        using (var scope = sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Lists.Remove(await db.Lists.FirstAsync(l => l.Id == listId));
            await db.SaveChangesAsync();
        }

        using (var scope = sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var route = await db.Routes.Include(r => r.Stops).FirstOrDefaultAsync(r => r.Id == routeId);
            Assert.NotNull(route);                                  // route survives
            Assert.Single(route!.Stops);                            // stop survives, still drivable
            Assert.Equal("500 Cedar St", route.Stops.First().SnapshotAddress);
        }
    }
}
