using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TourList.Server.Mapping;
using TourList.Server.Services;
using TourList.Shared;
using TourList.Shared.Entries;

namespace TourList.Server.Controllers;

/// <summary>Entry management (admin only): create/edit/delete, flagged resolution, CSV import.</summary>
[ApiController]
[Route("api")]
[Authorize(Roles = RoleNames.Admin)]
public sealed class EntriesController(EntryService entries) : ControllerBase
{
    [HttpPost("lists/{listId:guid}/entries")]
    public async Task<ActionResult<EntryDto>> Create(Guid listId, CreateEntryRequest request, CancellationToken ct)
    {
        var entry = await entries.CreateAsync(listId, request.RawAddress, request.Notes, ct);
        return entry is null ? NotFound() : Ok(entry.ToDto());
    }

    [HttpPut("entries/{id:guid}")]
    public async Task<ActionResult<EntryDto>> Update(Guid id, UpdateEntryRequest request, CancellationToken ct)
    {
        var entry = await entries.UpdateAsync(id, request.RawAddress, request.Notes, ct);
        return entry is null ? NotFound() : Ok(entry.ToDto());
    }

    [HttpPost("entries/{id:guid}/locate")]
    public async Task<ActionResult<EntryDto>> Locate(Guid id, LocateEntryRequest request, CancellationToken ct)
    {
        if (!request.HasManualPin && !request.HasCorrectedAddress)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Provide either coordinates (Latitude + Longitude) or a corrected address.");
        }

        var entry = await entries.LocateAsync(id, request, ct);
        return entry is null ? NotFound() : Ok(entry.ToDto());
    }

    [HttpDelete("entries/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        await entries.DeleteAsync(id, ct) ? NoContent() : NotFound();

    [HttpGet("entries/flagged")]
    public async Task<ActionResult<IReadOnlyList<EntryDto>>> Flagged(CancellationToken ct)
    {
        var flagged = await entries.GetFlaggedAsync(ct);
        return Ok(flagged.Select(e => e.ToDto()).ToList());
    }

    [HttpPost("lists/{listId:guid}/entries/import")]
    public async Task<ActionResult<CsvImportResult>> Import(Guid listId, IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "No CSV file was provided.");
        }

        try
        {
            await using var stream = file.OpenReadStream();
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var result = await entries.ImportCsvAsync(listId, reader, ct);
            return result is null ? NotFound() : Ok(result);
        }
        catch (CsvImportFormatException ex)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: ex.Message);
        }
    }
}
