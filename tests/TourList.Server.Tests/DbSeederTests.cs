using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TourList.Server.Data;
using TourList.Server.Options;
using TourList.Server.Tests.Infrastructure;
using TourList.Shared;

namespace TourList.Server.Tests;

[Collection(SqlServerCollection.Name)]
public sealed class DbSeederTests(SqlServerContainerFixture fixture)
{
    private const string AdminEmail = "seed-admin@tourlist.local";

    private ServiceProvider BuildServices() =>
        new ServiceCollection()
            .AddLogging()
            .AddDbContext<AppDbContext>(o => o.UseSqlServer(fixture.ConnectionStringFor("tl_seed_tests")))
            .AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>()
            .Services
            .Configure<AdminBootstrapOptions>(o =>
            {
                o.Email = AdminEmail;
                o.Password = "Seed!Passw0rd";
            })
            .BuildServiceProvider();

    [Fact]
    public async Task Seed_creates_roles_and_bootstrap_admin_idempotently()
    {
        await using var sp = BuildServices();

        await using (var scope = sp.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
        }

        // First seed populates roles + the bootstrap admin.
        using (var scope = sp.CreateScope())
        {
            await DbSeeder.SeedAsync(scope.ServiceProvider);
        }

        using (var scope = sp.CreateScope())
        {
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            Assert.True(await roleManager.RoleExistsAsync(RoleNames.User));
            Assert.True(await roleManager.RoleExistsAsync(RoleNames.Admin));

            var admin = await userManager.FindByEmailAsync(AdminEmail);
            Assert.NotNull(admin);
            Assert.True(admin!.IsActive);
            Assert.True(admin.EmailConfirmed);
            Assert.True(await userManager.IsInRoleAsync(admin, RoleNames.Admin));
        }

        // Second seed is a no-op: still exactly one admin.
        using (var scope = sp.CreateScope())
        {
            await DbSeeder.SeedAsync(scope.ServiceProvider);
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var admins = await userManager.GetUsersInRoleAsync(RoleNames.Admin);
            Assert.Single(admins);
        }
    }
}
