using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TourList.Server.Extensions;
using TourList.Server.Services;
using TourList.Shared.Routes;

namespace TourList.Server.Controllers;

/// <summary>Registered users' synced routes — owner-scoped (never-owned rows return 404).</summary>
[ApiController]
[Route("api/routes")]
[Authorize]
public sealed class RoutesController(RouteService routes, RouteMergeService merge) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RouteDto>>> GetAll(CancellationToken ct)
    {
        var userId = User.GetUserId()!.Value;
        return Ok(await routes.GetAllForUserAsync(userId, ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RouteDto>> Get(Guid id, CancellationToken ct)
    {
        var userId = User.GetUserId()!.Value;
        var route = await routes.GetForUserAsync(userId, id, ct);
        return route is null ? NotFound() : Ok(route);
    }

    [HttpPost]
    public async Task<ActionResult<RouteDto>> Create(RouteDto route, CancellationToken ct)
    {
        var userId = User.GetUserId()!.Value;
        var result = await routes.UpsertAsync(userId, route, routeId: null, ct);
        return ToActionResult(result, id => CreatedAtAction(nameof(Get), new { id }, result.Route));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<RouteDto>> Upsert(Guid id, RouteDto route, CancellationToken ct)
    {
        var userId = User.GetUserId()!.Value;
        // The URL id is authoritative (the route template). Passed explicitly so the immutable DTO
        // is not mutated in place.
        var result = await routes.UpsertAsync(userId, route, id, ct);
        return ToActionResult(result, _ => Ok(result.Route));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var userId = User.GetUserId()!.Value;
        return await routes.DeleteAsync(userId, id, ct) ? NoContent() : NotFound();
    }

    [HttpPost("merge")]
    public async Task<ActionResult<MergeRoutesResponse>> Merge(MergeRoutesRequest request, CancellationToken ct)
    {
        var userId = User.GetUserId()!.Value;
        return Ok(await merge.MergeAsync(userId, request.Routes, ct));
    }

    private ActionResult<RouteDto> ToActionResult(UpsertResult result, Func<Guid, ActionResult<RouteDto>> onOk) =>
        result.Status switch
        {
            UpsertStatus.Ok => onOk(result.Route!.Id),
            // Route id belongs to another user — hide its existence.
            UpsertStatus.Forbidden => NotFound(),
            UpsertStatus.NameConflict => Conflict(new { message = "A route with that name already exists." }),
            _ => StatusCode(StatusCodes.Status500InternalServerError),
        };
}
