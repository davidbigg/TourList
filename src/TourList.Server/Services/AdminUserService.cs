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

        var adminIds = new HashSet<Guid>();
        if (await AdminRoleIdAsync(ct) is Guid roleId)
        {
            var ids = await db.UserRoles.Where(ur => ur.RoleId == roleId).Select(ur => ur.UserId).ToListAsync(ct);
            adminIds = ids.ToHashSet();
        }

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
        MutateAsync(userId, guard: false, async (user, _) =>
        {
            if (!await users.IsInRoleAsync(user, RoleNames.Admin))
            {
                await users.AddToRoleAsync(user, RoleNames.Admin);
            }
        }, ct);

    public Task<AdminActionOutcome> DemoteAsync(Guid userId, CancellationToken ct) =>
        MutateAsync(userId, guard: true, async (user, _) =>
        {
            if (await users.IsInRoleAsync(user, RoleNames.Admin))
            {
                await users.RemoveFromRoleAsync(user, RoleNames.Admin);
            }
        }, ct);

    public Task<AdminActionOutcome> DeactivateAsync(Guid userId, CancellationToken ct) =>
        MutateAsync(userId, guard: true, async (user, _) =>
        {
            user.IsActive = false;
            await users.UpdateAsync(user);
            await users.UpdateSecurityStampAsync(user); // reject outstanding cookies + block login
        }, ct);

    public Task<AdminActionOutcome> ReactivateAsync(Guid userId, CancellationToken ct) =>
        MutateAsync(userId, guard: false, async (user, _) =>
        {
            user.IsActive = true;
            await users.UpdateAsync(user);
        }, ct);

    public Task<AdminActionOutcome> RemoveAsync(Guid userId, CancellationToken ct) =>
        MutateAsync(userId, guard: true, async (user, _) =>
        {
            await users.DeleteAsync(user); // FK cascade purges the user's routes
        }, ct);

    /// <summary>
    /// Runs check+mutate in a serializable transaction. When <paramref name="guard"/> is set and the
    /// target is an active admin, the mutation is blocked unless another active admin remains.
    /// </summary>
    /// <remarks>
    /// The <paramref name="mutate"/> delegate is handed the <see cref="CancellationToken"/>, but the
    /// role/stamp operations in the closures go through <see cref="UserManager{TUser}"/>, whose public
    /// API exposes no per-call token — so those calls discard it and run under UserManager's own token.
    /// The token IS honored by the guard queries and the surrounding transaction here.
    /// </remarks>
    private async Task<AdminActionOutcome> MutateAsync(
        Guid userId, bool guard, Func<ApplicationUser, CancellationToken, Task> mutate, CancellationToken ct)
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

            if (guard && user.IsActive && await WouldStrandActiveAdminsAsync(userId, ct))
            {
                return AdminActionOutcome.WouldRemoveLastActiveAdmin;
            }

            await mutate(user, ct);
            await tx.CommitAsync(ct);
            return AdminActionOutcome.Ok;
        });
    }

    /// <summary>
    /// True when <paramref name="userId"/> is an active admin and no <i>other</i> active admin remains
    /// — i.e. demoting / deactivating / removing them would drop the active-admin count to zero. Runs
    /// as ct-aware EF queries against the Identity role tables (UserManager's role APIs take no token),
    /// inside the caller's serializable transaction.
    /// </summary>
    private async Task<bool> WouldStrandActiveAdminsAsync(Guid userId, CancellationToken ct)
    {
        if (await AdminRoleIdAsync(ct) is not Guid roleId)
        {
            return false; // no Admin role exists → nothing to strand
        }

        var targetIsAdmin = await db.UserRoles.AnyAsync(ur => ur.UserId == userId && ur.RoleId == roleId, ct);
        if (!targetIsAdmin)
        {
            return false;
        }

        var otherActiveAdmins = await db.Users.CountAsync(
            u => u.Id != userId && u.IsActive && db.UserRoles.Any(ur => ur.UserId == u.Id && ur.RoleId == roleId),
            ct);
        return otherActiveAdmins == 0;
    }

    private async Task<Guid?> AdminRoleIdAsync(CancellationToken ct)
    {
        var normalizedName = users.NormalizeName(RoleNames.Admin);
        return await db.Roles
            .Where(r => r.NormalizedName == normalizedName)
            .Select(r => (Guid?)r.Id)
            .FirstOrDefaultAsync(ct);
    }
}
