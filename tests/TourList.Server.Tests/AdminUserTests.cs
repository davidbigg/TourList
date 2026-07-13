using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TourList.Server.Data;
using TourList.Server.Services;
using TourList.Server.Tests.Infrastructure;
using TourList.Shared;

namespace TourList.Server.Tests;

/// <summary>Admin user management: last-admin invariant + deactivate/remove semantics (scenarios 9, 12).</summary>
[Collection(SqlServerCollection.Name)]
public sealed class AdminUserTests(SqlServerContainerFixture fixture)
{
    // Unique DB per test instance — the last-admin invariant is global, so tests must not share state.
    private readonly string _dbName = $"tl_adminuser_{Guid.NewGuid():N}";

    private ServiceProvider BuildServices() =>
        new ServiceCollection()
            .AddLogging()
            .AddDbContext<AppDbContext>(o => o.UseSqlServer(fixture.ConnectionStringFor(_dbName)))
            .AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>()
            .Services
            .AddScoped<AdminUserService>()
            .BuildServiceProvider();

    private static async Task<Guid> CreateUserAsync(IServiceProvider sp, bool admin)
    {
        var roles = sp.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        if (!await roles.RoleExistsAsync(RoleNames.Admin))
        {
            await roles.CreateAsync(new IdentityRole<Guid>(RoleNames.Admin));
        }

        var users = sp.GetRequiredService<UserManager<ApplicationUser>>();
        var email = $"user-{Guid.NewGuid():N}@tourlist.local";
        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, IsActive = true };
        await users.CreateAsync(user, "Passw0rd!23");
        if (admin)
        {
            await users.AddToRoleAsync(user, RoleNames.Admin);
        }
        return user.Id;
    }

    [Fact] // scenario 12
    public async Task Cannot_demote_deactivate_or_remove_the_only_active_admin()
    {
        await using var sp = BuildServices();
        await Migrate(sp);
        var onlyAdmin = await CreateUserAsync(sp, admin: true);

        using var scope = sp.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<AdminUserService>();

        Assert.Equal(AdminActionOutcome.WouldRemoveLastActiveAdmin, await svc.DemoteAsync(onlyAdmin, default));
        Assert.Equal(AdminActionOutcome.WouldRemoveLastActiveAdmin, await svc.DeactivateAsync(onlyAdmin, default));
        Assert.Equal(AdminActionOutcome.WouldRemoveLastActiveAdmin, await svc.RemoveAsync(onlyAdmin, default));
    }

    [Fact]
    public async Task Can_demote_an_admin_while_another_active_admin_remains()
    {
        await using var sp = BuildServices();
        await Migrate(sp);
        var first = await CreateUserAsync(sp, admin: true);
        var second = await CreateUserAsync(sp, admin: true);

        using var scope = sp.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<AdminUserService>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        Assert.Equal(AdminActionOutcome.Ok, await svc.DemoteAsync(first, default));
        Assert.False(await users.IsInRoleAsync((await users.FindByIdAsync(first.ToString()))!, RoleNames.Admin));

        // Now only `second` remains — it can't be demoted.
        Assert.Equal(AdminActionOutcome.WouldRemoveLastActiveAdmin, await svc.DemoteAsync(second, default));
    }

    [Fact] // scenario 9
    public async Task Promote_grants_the_admin_role()
    {
        await using var sp = BuildServices();
        await Migrate(sp);
        await CreateUserAsync(sp, admin: true); // keep the invariant satisfiable
        var user = await CreateUserAsync(sp, admin: false);

        using var scope = sp.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<AdminUserService>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        Assert.Equal(AdminActionOutcome.Ok, await svc.PromoteAsync(user, default));
        Assert.True(await users.IsInRoleAsync((await users.FindByIdAsync(user.ToString()))!, RoleNames.Admin));
    }

    [Fact]
    public async Task Deactivate_keeps_routes_and_bumps_security_stamp()
    {
        await using var sp = BuildServices();
        await Migrate(sp);
        var keepAdmin = await CreateUserAsync(sp, admin: true); // another active admin
        var target = await CreateUserAsync(sp, admin: true);

        // Give the target a route.
        using (var scope = sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Routes.Add(new Data.Entities.SavedRoute { Id = Guid.NewGuid(), OwnerUserId = target, Name = "Kept" });
            await db.SaveChangesAsync();
        }

        string stampBefore;
        using (var scope = sp.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            stampBefore = (await users.FindByIdAsync(target.ToString()))!.SecurityStamp!;
            var svc = scope.ServiceProvider.GetRequiredService<AdminUserService>();
            Assert.Equal(AdminActionOutcome.Ok, await svc.DeactivateAsync(target, default));
        }

        using (var scope = sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var reloaded = (await users.FindByIdAsync(target.ToString()))!;

            Assert.False(reloaded.IsActive);
            Assert.NotEqual(stampBefore, reloaded.SecurityStamp);        // cookies invalidated
            Assert.Equal(1, await db.Routes.CountAsync(r => r.OwnerUserId == target)); // routes retained
        }

        _ = keepAdmin;
    }

    [Fact]
    public async Task Remove_cascade_purges_the_users_routes()
    {
        await using var sp = BuildServices();
        await Migrate(sp);
        await CreateUserAsync(sp, admin: true); // satisfy the invariant
        var target = await CreateUserAsync(sp, admin: false);

        using (var scope = sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Routes.Add(new Data.Entities.SavedRoute { Id = Guid.NewGuid(), OwnerUserId = target, Name = "Doomed" });
            await db.SaveChangesAsync();
        }

        using (var scope = sp.CreateScope())
        {
            var svc = scope.ServiceProvider.GetRequiredService<AdminUserService>();
            Assert.Equal(AdminActionOutcome.Ok, await svc.RemoveAsync(target, default));
        }

        using (var scope = sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(0, await db.Routes.CountAsync(r => r.OwnerUserId == target));
            Assert.False(await db.Users.AnyAsync(u => u.Id == target));
        }
    }

    private static async Task Migrate(IServiceProvider sp)
    {
        using var scope = sp.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    }
}
