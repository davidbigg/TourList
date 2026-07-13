using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TourList.Server.Services;
using TourList.Shared;
using TourList.Shared.Admin;

namespace TourList.Server.Controllers;

/// <summary>Admin user management. All mutations are guarded by the last-admin invariant (409).</summary>
[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = RoleNames.Admin)]
public sealed class AdminUsersController(AdminUserService admins) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminUserDto>>> GetUsers(CancellationToken ct) =>
        Ok(await admins.ListAsync(ct));

    [HttpPost("{id:guid}/promote")]
    public async Task<IActionResult> Promote(Guid id, CancellationToken ct) => Map(await admins.PromoteAsync(id, ct));

    [HttpPost("{id:guid}/demote")]
    public async Task<IActionResult> Demote(Guid id, CancellationToken ct) => Map(await admins.DemoteAsync(id, ct));

    [HttpPost("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct) => Map(await admins.DeactivateAsync(id, ct));

    [HttpPost("{id:guid}/reactivate")]
    public async Task<IActionResult> Reactivate(Guid id, CancellationToken ct) => Map(await admins.ReactivateAsync(id, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Remove(Guid id, CancellationToken ct) => Map(await admins.RemoveAsync(id, ct));

    private IActionResult Map(AdminActionOutcome outcome) => outcome switch
    {
        AdminActionOutcome.Ok => NoContent(),
        AdminActionOutcome.NotFound => NotFound(),
        AdminActionOutcome.WouldRemoveLastActiveAdmin => Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "This would remove the last active admin.",
            detail: "The system must always retain at least one active administrator."),
        _ => StatusCode(StatusCodes.Status500InternalServerError),
    };
}
