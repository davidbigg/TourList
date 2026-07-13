using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TourList.Server.Data;
using TourList.Server.Services;
using TourList.Server.Tests.Infrastructure;
using TourList.Shared.Routes;

namespace TourList.Server.Tests;

/// <summary>Guest→account merge algorithm (acceptance scenarios 8 + 13) against real SQL Server.</summary>
[Collection(SqlServerCollection.Name)]
public sealed class RouteMergeTests(SqlServerContainerFixture fixture)
{
    private ServiceProvider BuildServices() =>
        new ServiceCollection()
            .AddLogging()
            .AddDbContext<AppDbContext>(o => o.UseSqlServer(fixture.ConnectionStringFor("tl_merge_tests")))
            .AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>()
            .Services
            .AddScoped<RouteService>()
            .AddScoped<RouteMergeService>()
            .BuildServiceProvider();

    private static async Task<Guid> CreateUserAsync(IServiceProvider sp)
    {
        var users = sp.GetRequiredService<UserManager<ApplicationUser>>();
        var email = $"driver-{Guid.NewGuid():N}@tourlist.local";
        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, IsActive = true };
        var result = await users.CreateAsync(user, "Passw0rd!23");
        Assert.True(result.Succeeded);
        return user.Id;
    }

    private static RouteDto Route(string name, params (string Address, double Lat, double Lng, string? Notes)[] stops) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Stops = stops.Select((s, i) => new RouteStopDto
        {
            Id = Guid.NewGuid(),
            Order = i,
            SnapshotAddress = s.Address,
            SnapshotLatitude = s.Lat,
            SnapshotLongitude = s.Lng,
            SnapshotNotes = s.Notes,
        }).ToList(),
    };

    [Fact]
    public async Task Merge_creates_guest_routes_and_is_one_time()
    {
        await using var sp = BuildServices();
        await Migrate(sp);
        var userId = await CreateUserAsync(sp);

        using (var scope = sp.CreateScope())
        {
            var merge = scope.ServiceProvider.GetRequiredService<RouteMergeService>();
            var response = await merge.MergeAsync(userId, [Route("Morning loop", ("1 Main St", 47.6, -122.3, "note"))], default);

            Assert.False(response.AlreadyMigrated);
            Assert.Equal(1, response.Counts.Created);
        }

        // Second merge on the same account is a no-op.
        using (var scope = sp.CreateScope())
        {
            var merge = scope.ServiceProvider.GetRequiredService<RouteMergeService>();
            var again = await merge.MergeAsync(userId, [Route("Another", ("2 Oak Ave", 47.7, -122.2, null))], default);
            Assert.True(again.AlreadyMigrated);
        }

        using (var scope = sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var names = await db.Routes.Where(r => r.OwnerUserId == userId).Select(r => r.Name).ToListAsync();
            Assert.Single(names);
            Assert.Equal("Morning loop", names[0]);
        }
    }

    [Fact]
    public async Task Merge_dedupes_identical_content_even_with_a_different_name()
    {
        await using var sp = BuildServices();
        await Migrate(sp);
        var userId = await CreateUserAsync(sp);

        // Pre-existing account route.
        using (var scope = sp.CreateScope())
        {
            var svc = scope.ServiceProvider.GetRequiredService<RouteService>();
            await svc.UpsertAsync(userId, Route("Original name", ("10 Pine St", 47.65, -122.35, "x")), default);
        }

        using (var scope = sp.CreateScope())
        {
            var merge = scope.ServiceProvider.GetRequiredService<RouteMergeService>();
            // Same stops/content, different name → should dedupe, not create.
            var response = await merge.MergeAsync(userId, [Route("Different name", ("10 Pine St", 47.65, -122.35, "x"))], default);

            Assert.Equal(1, response.Counts.Deduped);
            Assert.Equal(0, response.Counts.Created);
            Assert.Equal(RouteMergeOutcome.DedupedExisting, response.Results[0].Outcome);
        }

        using (var scope = sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(1, await db.Routes.CountAsync(r => r.OwnerUserId == userId));
        }
    }

    [Fact] // scenario 13
    public async Task Merge_renames_on_name_collision_and_keeps_both()
    {
        await using var sp = BuildServices();
        await Migrate(sp);
        var userId = await CreateUserAsync(sp);

        using (var scope = sp.CreateScope())
        {
            var svc = scope.ServiceProvider.GetRequiredService<RouteService>();
            await svc.UpsertAsync(userId, Route("Trip", ("100 First Ave", 47.60, -122.33, "original")), default);
        }

        using (var scope = sp.CreateScope())
        {
            var merge = scope.ServiceProvider.GetRequiredService<RouteMergeService>();
            // Same name, DIFFERENT content → kept as a renamed copy; nothing overwritten.
            var response = await merge.MergeAsync(userId, [Route("Trip", ("200 Second Ave", 47.62, -122.31, "incoming"))], default);

            Assert.Equal(1, response.Counts.Renamed);
            Assert.Equal(RouteMergeOutcome.RenamedCopy, response.Results[0].Outcome);
            Assert.Equal("Trip (imported)", response.Results[0].FinalName);
        }

        using (var scope = sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var routes = await db.Routes.Include(r => r.Stops).Where(r => r.OwnerUserId == userId).ToListAsync();
            Assert.Equal(2, routes.Count);
            // Original untouched.
            var original = routes.Single(r => r.Name == "Trip");
            Assert.Equal("100 First Ave", original.Stops.Single().SnapshotAddress);
            // Incoming kept under a disambiguated name.
            var imported = routes.Single(r => r.Name == "Trip (imported)");
            Assert.Equal("200 Second Ave", imported.Stops.Single().SnapshotAddress);
        }
    }

    private static async Task Migrate(IServiceProvider sp)
    {
        using var scope = sp.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    }
}
