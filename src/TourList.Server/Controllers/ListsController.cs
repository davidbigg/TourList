using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TourList.Server.Data;
using TourList.Server.Data.Entities;
using TourList.Server.Extensions;
using TourList.Server.Mapping;
using TourList.Shared;
using TourList.Shared.Entries;
using TourList.Shared.Lists;

namespace TourList.Server.Controllers;

/// <summary>Content lists: public read, admin write.</summary>
[ApiController]
[Route("api/lists")]
public sealed class ListsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<ListDto>>> GetLists(CancellationToken ct)
    {
        var lists = await db.Lists
            .OrderBy(l => l.Name)
            .Select(l => new ListDto
            {
                Id = l.Id,
                Name = l.Name,
                EntryCount = l.Entries.Count,
                CreatedUtc = l.CreatedUtc,
                UpdatedUtc = l.UpdatedUtc,
            })
            .ToListAsync(ct);

        return Ok(lists);
    }

    [HttpGet("{id:guid}/entries")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<EntryDto>>> GetEntries(Guid id, CancellationToken ct)
    {
        if (!await db.Lists.AnyAsync(l => l.Id == id, ct))
        {
            return NotFound();
        }

        var entries = await db.Entries
            .Where(e => e.ListId == id)
            .OrderBy(e => e.CreatedUtc)
            .ToListAsync(ct);

        return Ok(entries.Select(e => e.ToDto()).ToList());
    }

    [HttpPost]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<ActionResult<ListDto>> Create(CreateListRequest request, CancellationToken ct)
    {
        var list = new AddressList
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            CreatedByUserId = User.GetUserId(),
        };

        db.Lists.Add(list);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetEntries), new { id = list.Id }, list.ToDto(0));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<ActionResult<ListDto>> Update(Guid id, UpdateListRequest request, CancellationToken ct)
    {
        var list = await db.Lists.FirstOrDefaultAsync(l => l.Id == id, ct);
        if (list is null)
        {
            return NotFound();
        }

        list.Name = request.Name.Trim();
        list.UpdatedUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var entryCount = await db.Entries.CountAsync(e => e.ListId == id, ct);
        return Ok(list.ToDto(entryCount));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var list = await db.Lists.FirstOrDefaultAsync(l => l.Id == id, ct);
        if (list is null)
        {
            return NotFound();
        }

        // Cascades to the list's entries only — never to any saved route (snapshots).
        db.Lists.Remove(list);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
