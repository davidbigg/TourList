using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using TourList.Shared.Entries;

namespace TourList.Server.Services;

/// <summary>A row that parsed into a usable address.</summary>
public sealed record ParsedCsvRow(int RowNumber, string Address, string? Notes);

/// <summary>Parsed rows plus per-row parse failures (rows that produced no entry).</summary>
public sealed record CsvParseOutcome(List<ParsedCsvRow> Rows, List<CsvParseFailure> Failures);

/// <summary>Thrown when the whole file is unusable (e.g. the required header is missing).</summary>
public sealed class CsvImportFormatException(string message) : Exception(message);

/// <summary>
/// Parses the v1 import CSV: required <c>Address</c> header, optional <c>Notes</c> header, UTF-8.
/// Pure and unit-testable (feed it a <see cref="TextReader"/>). Row numbers are 1-based and include
/// the header row, matching what a user sees in a spreadsheet.
/// </summary>
public static class CsvEntryParser
{
    public static CsvParseOutcome Parse(TextReader reader)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null,
            HeaderValidated = null,
            TrimOptions = TrimOptions.Trim,
            IgnoreBlankLines = true,
        };

        using var csv = new CsvReader(reader, config);

        if (!csv.Read() || !csv.ReadHeader())
        {
            throw new CsvImportFormatException("The CSV file is empty or has no header row.");
        }

        var headers = csv.HeaderRecord ?? [];
        var hasAddress = headers.Any(h => string.Equals(h, "Address", StringComparison.OrdinalIgnoreCase));
        if (!hasAddress)
        {
            throw new CsvImportFormatException("The CSV must contain a required 'Address' header column.");
        }

        var hasNotes = headers.Any(h => string.Equals(h, "Notes", StringComparison.OrdinalIgnoreCase));

        var rows = new List<ParsedCsvRow>();
        var failures = new List<CsvParseFailure>();

        while (csv.Read())
        {
            var rowNumber = csv.Parser.Row;
            var address = csv.GetField("Address");
            var notes = hasNotes ? csv.GetField("Notes") : null;

            if (string.IsNullOrWhiteSpace(address))
            {
                // A line that is entirely blank / delimiters only (e.g. a trailing newline) is skipped,
                // not reported. A line with other content but no address is a genuine parse failure.
                var raw = csv.Parser.RawRecord ?? string.Empty;
                var isBlankLine = string.IsNullOrWhiteSpace(raw.Replace(",", string.Empty));
                if (isBlankLine)
                {
                    continue;
                }

                failures.Add(new CsvParseFailure
                {
                    RowNumber = rowNumber,
                    RawLine = raw.Trim(),
                    Reason = "Missing required address value.",
                });
                continue;
            }

            rows.Add(new ParsedCsvRow(
                rowNumber,
                address.Trim(),
                string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()));
        }

        return new CsvParseOutcome(rows, failures);
    }
}
