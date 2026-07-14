using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using TourList.Server.Data;
using TourList.Server.Data.Entities;
using TourList.Server.Services;
using TourList.Server.Tests.Infrastructure;
using TourList.Shared.Routes;

namespace TourList.Server.Tests;

/// <summary>
/// The (OwnerUserId, Name) uniqueness check in <see cref="RouteService"/> is best-effort (TOCTOU).
/// When a concurrent writer wins the race the filtered unique index fires; that must surface as a
/// NameConflict (→ 409), never an unhandled DbUpdateException (→ 500). A SaveChanges interceptor
/// deterministically plays the losing side of the race.
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class RouteNameConflictTests(SqlServerContainerFixture fixture)
{
    [Fact]
    public async Task Upsert_that_loses_the_name_race_returns_NameConflict_not_a_server_error()
    {
        var connectionString = fixture.ConnectionStringFor("tl_routeconflict_tests");
        var interceptor = new RaceInjectingInterceptor(connectionString);

        await using var sp = BuildServices(connectionString, interceptor);
        await Migrate(sp);
        var userId = await CreateUserAsync(sp);

        using var scope = sp.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<RouteService>();

        // The app-level AnyAsync check passes (no such name yet); the interceptor then commits a
        // conflicting (OwnerUserId, Name) row via a separate connection just before our insert runs,
        // so the insert violates the filtered unique index.
        var result = await svc.UpsertAsync(
            userId,
            Route("Shared name", ("1 Main St", 47.6, -122.3, null)),
            routeId: null,
            default);

        Assert.True(interceptor.Fired); // the race was actually injected
        Assert.Equal(UpsertStatus.NameConflict, result.Status);
        Assert.Null(result.Route);
    }

    private static ServiceProvider BuildServices(string connectionString, IInterceptor interceptor) =>
        new ServiceCollection()
            .AddLogging()
            .AddDbContext<AppDbContext>(o => o.UseSqlServer(connectionString).AddInterceptors(interceptor))
            .AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>()
            .Services
            .AddScoped<RouteService>()
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

    private static async Task Migrate(IServiceProvider sp)
    {
        using var scope = sp.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    }

    /// <summary>
    /// On the first SaveChanges that inserts a <see cref="SavedRoute"/>, commits a row with the same
    /// (OwnerUserId, Name) via an independent connection — simulating a concurrent writer that wins
    /// the race, so the intercepted insert then trips the filtered unique index.
    /// </summary>
    private sealed class RaceInjectingInterceptor(string connectionString) : SaveChangesInterceptor
    {
        public bool Fired { get; private set; }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (!Fired && eventData.Context is not null)
            {
                var added = eventData.Context.ChangeTracker.Entries<SavedRoute>()
                    .FirstOrDefault(e => e.State == EntityState.Added);
                if (added is not null)
                {
                    Fired = true;

                    var options = new DbContextOptionsBuilder<AppDbContext>()
                        .UseSqlServer(connectionString)
                        .Options;
                    await using var other = new AppDbContext(options);
                    other.Routes.Add(new SavedRoute
                    {
                        Id = Guid.NewGuid(),
                        OwnerUserId = added.Entity.OwnerUserId,
                        Name = added.Entity.Name,
                        CreatedUtc = DateTime.UtcNow,
                        UpdatedUtc = DateTime.UtcNow,
                    });
                    await other.SaveChangesAsync(ct);
                }
            }

            return result;
        }
    }
}
