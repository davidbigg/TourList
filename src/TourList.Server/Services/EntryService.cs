using Microsoft.EntityFrameworkCore;
using TourList.Server.Data;
using TourList.Server.Data.Entities;
using TourList.Server.Mapping;
using TourList.Server.Services.Geocoding;
using TourList.Shared.Entries;
using TourList.Shared.Enums;

namespace TourList.Server.Services;

/// <summary>
/// Entry lifecycle + the geocoding gate orchestration. Applies <see cref="GeocodeResult"/> to the
/// entity: Located sets coords, Unlocatable flags for admin, Skipped leaves it Pending.
/// </summary>
public sealed class EntryService(AppDbContext db, IGeocoder geocoder)
{
    public async Task<Entry?> CreateAsync(Guid listId, string rawAddress, string? notes, CancellationToken ct)
    {
        var listExists = await db.Lists.AnyAsync(l => l.Id == listId, ct);
        if (!listExists)
        {
            return null;
        }

        var entry = new Entry
        {
            Id = Guid.NewGuid(),
            ListId = listId,
            RawAddress = rawAddress.Trim(),
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
        };

        ApplyGeocode(entry, await geocoder.GeocodeAsync(entry.RawAddress, ct));
        db.Entries.Add(entry);
        await db.SaveChangesAsync(ct);
        return entry;
    }

    public async Task<Entry?> UpdateAsync(Guid entryId, string rawAddress, string? notes, CancellationToken ct)
    {
        var entry = await db.Entries.FirstOrDefaultAsync(e => e.Id == entryId, ct);
        if (entry is null)
        {
            return null;
        }

        var normalized = rawAddress.Trim();
        var addressChanged = !string.Equals(entry.RawAddress, normalized, StringComparison.Ordinal);

        entry.RawAddress = normalized;
        entry.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        entry.UpdatedUtc = DateTime.UtcNow;

        // Re-geocode only when the address actually changed (spec: editing an address re-resolves).
        if (addressChanged)
        {
            ApplyGeocode(entry, await geocoder.GeocodeAsync(normalized, ct));
        }

        await db.SaveChangesAsync(ct);
        return entry;
    }

    public async Task<Entry?> LocateAsync(Guid entryId, LocateEntryRequest request, CancellationToken ct)
    {
        var entry = await db.Entries.FirstOrDefaultAsync(e => e.Id == entryId, ct);
        if (entry is null)
        {
            return null;
        }

        if (request.HasManualPin)
        {
            // Admin dropped a pin / entered coordinates.
            entry.Latitude = request.Latitude;
            entry.Longitude = request.Longitude;
            entry.GeocodeStatus = GeocodeStatus.Located;
            entry.ManuallyLocated = true;
            entry.GeocodeConfidence = null;
            entry.LastGeocodedUtc = DateTime.UtcNow;
        }
        else if (request.HasCorrectedAddress)
        {
            entry.RawAddress = request.CorrectedAddress!.Trim();
            ApplyGeocode(entry, await geocoder.GeocodeAsync(entry.RawAddress, ct));
        }

        entry.UpdatedUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return entry;
    }

    public async Task<bool> DeleteAsync(Guid entryId, CancellationToken ct)
    {
        var entry = await db.Entries.FirstOrDefaultAsync(e => e.Id == entryId, ct);
        if (entry is null)
        {
            return false;
        }

        db.Entries.Remove(entry);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<List<Entry>> GetFlaggedAsync(CancellationToken ct) =>
        await db.Entries
            .Where(e => e.GeocodeStatus == GeocodeStatus.Unlocatable)
            .OrderBy(e => e.CreatedUtc)
            .ToListAsync(ct);

    public async Task<CsvImportResult?> ImportCsvAsync(Guid listId, TextReader csv, CancellationToken ct)
    {
        var listExists = await db.Lists.AnyAsync(l => l.Id == listId, ct);
        if (!listExists)
        {
            return null;
        }

        var parsed = CsvEntryParser.Parse(csv);
        var parseFailures = new List<CsvParseFailure>(parsed.Failures);
        var duplicateWarnings = new List<CsvDuplicateWarning>();
        var geocodeFailures = new List<CsvGeocodeFailure>();
        var imported = new List<EntryDto>();

        var existingAddresses = await db.Entries
            .Where(e => e.ListId == listId)
            .Select(e => e.RawAddress)
            .ToListAsync(ct);
        var seen = new HashSet<string>(existingAddresses, StringComparer.OrdinalIgnoreCase);

        foreach (var row in parsed.Rows)
        {
            if (!seen.Add(row.Address))
            {
                duplicateWarnings.Add(new CsvDuplicateWarning
                {
                    RowNumber = row.RowNumber,
                    Address = row.Address,
                    Message = "Duplicate address in this list (allowed; deduping is left to the admin).",
                });
            }

            var entry = new Entry
            {
                Id = Guid.NewGuid(),
                ListId = listId,
                RawAddress = row.Address,
                Notes = row.Notes,
            };

            var geo = await geocoder.GeocodeAsync(row.Address, ct);
            ApplyGeocode(entry, geo);
            db.Entries.Add(entry);

            if (entry.GeocodeStatus == GeocodeStatus.Unlocatable)
            {
                geocodeFailures.Add(new CsvGeocodeFailure
                {
                    RowNumber = row.RowNumber,
                    EntryId = entry.Id,
                    Address = row.Address,
                    Reason = geo.Reason ?? "Could not be located.",
                });
            }
            else
            {
                imported.Add(entry.ToDto());
            }
        }

        await db.SaveChangesAsync(ct);

        return new CsvImportResult
        {
            Imported = imported,
            GeocodeFailures = geocodeFailures,
            ParseFailures = parseFailures,
            DuplicateWarnings = duplicateWarnings,
            Summary = new CsvImportSummary
            {
                TotalRows = parsed.Rows.Count + parsed.Failures.Count,
                Imported = imported.Count,
                GeocodeFailures = geocodeFailures.Count,
                ParseFailures = parseFailures.Count,
                DuplicateWarnings = duplicateWarnings.Count,
            },
        };
    }

    private static void ApplyGeocode(Entry entry, GeocodeResult result)
    {
        switch (result.Outcome)
        {
            case GeocodeOutcome.Located:
                entry.Latitude = result.Latitude;
                entry.Longitude = result.Longitude;
                entry.FormattedAddress = result.FormattedAddress;
                entry.GeocodeConfidence = result.Confidence;
                entry.GeocodeStatus = GeocodeStatus.Located;
                entry.ManuallyLocated = false;
                entry.LastGeocodedUtc = DateTime.UtcNow;
                break;

            case GeocodeOutcome.Unlocatable:
                entry.Latitude = null;
                entry.Longitude = null;
                entry.FormattedAddress = null;
                entry.GeocodeConfidence = null;
                entry.GeocodeStatus = GeocodeStatus.Unlocatable;
                entry.ManuallyLocated = false;
                entry.LastGeocodedUtc = DateTime.UtcNow;
                break;

            case GeocodeOutcome.Skipped:
                // Geocoding disabled — leave as Pending (not a failure); re-geocode when enabled.
                entry.GeocodeStatus = GeocodeStatus.Pending;
                break;
        }
    }
}
