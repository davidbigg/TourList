using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TourList.Server.Data;
using TourList.Shared;
using TourList.Shared.Admin;

namespace TourList.Server.Services;

public enum AdminActionOutcome
{
    Ok,
    NotFound,

    /// <summary>Blocked: the mutation would leave zero active admins (acceptance scenario 12).</summary>
    WouldRemoveLastActiveAdmin,
}

/// <summary>
/// Admin user management. Every mutation that could reduce the active-admin count
/// (demote/deactivate/remove of an <i>active admin</i>) recomputes the count excluding the target
/// inside a serializable transaction, blocking if it would hit zero. Deactivate keeps routes and
/// bumps the security stamp (invalidating cookies); remove deletes the user (cascade-purging routes).
/// </summary>
public sealed class AdminUserService(AppDbContext db, UserManager<ApplicationUser> users)
{
    public async Task<List<AdminUserDto>> ListAsync(CancellationToken ct)
    {
        var all = await db.Users.OrderBy(u => u.Email).ToListAsync(ct);
        var adminIds = (await users.GetUsersInRoleAsync(RoleNames.Admin)).Select(u => u.Id).ToHashSet();

        var routeCounts = await db.Routes
            .GroupBy(r => r.OwnerUserId)
            .Select(g => new { OwnerUserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.OwnerUserId, x => x.Count, ct);

        return all.Select(u => new AdminUserDto
        {
            Id = u.Id,
            Email = u.Email ?? string.Empty,
            IsAdmin = adminIds.Contains(u.Id),
            IsActive = u.IsActive,
            RouteCount = routeCounts.GetValueOrDefault(u.Id),
            CreatedUtc = u.CreatedUtc,
        }).ToList();
    }

    public Task<AdminActionOutcome> PromoteAsync(Guid userId, CancellationToken ct) =>
        MutateAsync(userId, guard: false, async user =>
        {
            if (!await users.IsInRoleAsync(user, RoleNames.Admin))
            {
                await users.AddToRoleAsync(user, RoleNames.Admin);
            }
        }, ct);

    public Task<AdminActionOutcome> DemoteAsync(Guid userId, CancellationToken ct) =>
        MutateAsync(userId, guard: true, async user =>
        {
            if (await users.IsInRoleAsync(user, RoleNames.Admin))
            {
                await users.RemoveFromRoleAsync(user, RoleNames.Admin);
            }
        }, ct);

    public Task<AdminActionOutcome> DeactivateAsync(Guid userId, CancellationToken ct) =>
        MutateAsync(userId, guard: true, async user =>
        {
            user.IsActive = false;
            await users.UpdateAsync(user);
            await users.UpdateSecurityStampAsync(user); // reject outstanding cookies + block login
        }, ct);

    public Task<AdminActionOutcome> ReactivateAsync(Guid userId, CancellationToken ct) =>
        MutateAsync(userId, guard: false, async user =>
        {
            user.IsActive = true;
            await users.UpdateAsync(user);
        }, ct);

    public Task<AdminActionOutcome> RemoveAsync(Guid userId, CancellationToken ct) =>
        MutateAsync(userId, guard: true, async user =>
        {
            await users.DeleteAsync(user); // FK cascade purges the user's routes
        }, ct);

    /// <summary>
    /// Runs check+mutate in a serializable transaction. When <paramref name="guard"/> is set and the
    /// target is an active admin, the mutation is blocked unless another active admin remains.
    /// </summary>
    private async Task<AdminActionOutcome> MutateAsync(Guid userId, bool guard, Func<ApplicationUser, Task> mutate, CancellationToken ct)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
            if (user is null)
            {
                return AdminActionOutcome.NotFound;
            }

            if (guard && user.IsActive && await users.IsInRoleAsync(user, RoleNames.Admin))
            {
                var admins = await users.GetUsersInRoleAsync(RoleNames.Admin);
                var otherActiveAdmins = admins.Count(a => a.Id != userId && a.IsActive);
                if (otherActiveAdmins == 0)
                {
                    return AdminActionOutcome.WouldRemoveLastActiveAdmin;
                }
            }

            await mutate(user);
            await tx.CommitAsync(ct);
            return AdminActionOutcome.Ok;
        });
    }
}
