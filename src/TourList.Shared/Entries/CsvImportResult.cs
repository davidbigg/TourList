namespace TourList.Shared.Entries;

/// <summary>
/// Outcome of a CSV import into a single list. Rows split into: created-and-located
/// (<see cref="Imported"/>), created-but-flagged (<see cref="GeocodeFailures"/>), and
/// not-created (<see cref="ParseFailures"/>). Intra-list duplicate addresses are allowed but
/// surfaced as <see cref="DuplicateWarnings"/> (deduping is left to the admin).
/// </summary>
public sealed class CsvImportResult
{
    public List<EntryDto> Imported { get; set; } = [];
    public List<CsvGeocodeFailure> GeocodeFailures { get; set; } = [];
    public List<CsvParseFailure> ParseFailures { get; set; } = [];
    public List<CsvDuplicateWarning> DuplicateWarnings { get; set; } = [];
    public CsvImportSummary Summary { get; set; } = new();
}

/// <summary>An entry was created from the row but flagged Unlocatable.</summary>
public sealed class CsvGeocodeFailure
{
    public int RowNumber { get; set; }
    public Guid EntryId { get; set; }
    public required string Address { get; set; }
    public required string Reason { get; set; }
}

/// <summary>A row could not be parsed into an entry; nothing was created.</summary>
public sealed class CsvParseFailure
{
    public int RowNumber { get; set; }
    public string? RawLine { get; set; }
    public required string Reason { get; set; }
}

/// <summary>A row's address duplicates another address already in the list (warning only).</summary>
public sealed class CsvDuplicateWarning
{
    public int RowNumber { get; set; }
    public required string Address { get; set; }
    public required string Message { get; set; }
}

public sealed class CsvImportSummary
{
    public int TotalRows { get; set; }
    public int Imported { get; set; }
    public int GeocodeFailures { get; set; }
    public int ParseFailures { get; set; }
    public int DuplicateWarnings { get; set; }
}
