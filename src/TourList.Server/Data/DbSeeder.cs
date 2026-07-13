using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using TourList.Server.Options;
using TourList.Shared;

namespace TourList.Server.Data;

/// <summary>
/// Ensures the <c>User</c>/<c>Admin</c> roles exist and bootstraps an initial admin on a fresh
/// system (acceptance scenario 1). Idempotent: the admin is created only when no admin exists.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services, CancellationToken ct = default)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DbSeeder).FullName!);

        foreach (var role in new[] { RoleNames.User, RoleNames.Admin })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));
            }
        }

        // Only bootstrap when the system has no admin at all.
        var admins = await userManager.GetUsersInRoleAsync(RoleNames.Admin);
        if (admins.Count > 0)
        {
            return;
        }

        var options = services.GetRequiredService<IOptions<AdminBootstrapOptions>>().Value;
        if (string.IsNullOrWhiteSpace(options.Email) || string.IsNullOrWhiteSpace(options.Password))
        {
            logger.LogWarning(
                "No admin exists and AdminBootstrap:Email/Password are not configured; skipping initial-admin seed.");
            return;
        }

        var existing = await userManager.FindByEmailAsync(options.Email);
        if (existing is null)
        {
            var admin = new ApplicationUser
            {
                UserName = options.Email,
                Email = options.Email,
                EmailConfirmed = true,
                IsActive = true,
            };

            var created = await userManager.CreateAsync(admin, options.Password);
            if (!created.Succeeded)
            {
                throw new InvalidOperationException(
                    "Failed to create bootstrap admin: " +
                    string.Join("; ", created.Errors.Select(e => $"{e.Code}:{e.Description}")));
            }

            existing = admin;
            logger.LogInformation("Seeded bootstrap admin {Email}.", options.Email);
        }

        if (!await userManager.IsInRoleAsync(existing, RoleNames.Admin))
        {
            await userManager.AddToRoleAsync(existing, RoleNames.Admin);
        }
    }
}
