namespace TourList.Shared.Entries;

/// <summary>
/// Outcome of a CSV import into a single list. Rows split into: created-and-located
/// (<see cref="Imported"/>), created-but-flagged (<see cref="GeocodeFailures"/>), and
/// not-created (<see cref="ParseFailures"/>). Intra-list duplicate addresses are allowed but
/// surfaced as <see cref="DuplicateWarnings"/> (deduping is left to the admin).
/// </summary>
public sealed class CsvImportResult
{
    public List<EntryDto> Imported { get; init; } = [];
    public List<CsvGeocodeFailure> GeocodeFailures { get; init; } = [];
    public List<CsvParseFailure> ParseFailures { get; init; } = [];
    public List<CsvDuplicateWarning> DuplicateWarnings { get; init; } = [];
    public CsvImportSummary Summary { get; init; } = new();
}

/// <summary>An entry was created from the row but flagged Unlocatable.</summary>
public sealed class CsvGeocodeFailure
{
    public int RowNumber { get; init; }
    public Guid EntryId { get; init; }
    public required string Address { get; init; }
    public required string Reason { get; init; }
}

/// <summary>A row could not be parsed into an entry; nothing was created.</summary>
public sealed class CsvParseFailure
{
    public int RowNumber { get; init; }
    public string? RawLine { get; init; }
    public required string Reason { get; init; }
}

/// <summary>A row's address duplicates another address already in the list (warning only).</summary>
public sealed class CsvDuplicateWarning
{
    public int RowNumber { get; init; }
    public required string Address { get; init; }
    public required string Message { get; init; }
}

public sealed class CsvImportSummary
{
    public int TotalRows { get; init; }
    public int Imported { get; init; }
    public int GeocodeFailures { get; init; }
    public int ParseFailures { get; init; }
    public int DuplicateWarnings { get; init; }
}
