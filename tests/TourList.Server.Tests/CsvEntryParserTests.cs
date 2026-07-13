using TourList.Server.Services;

namespace TourList.Server.Tests;

public sealed class CsvEntryParserTests
{
    private static CsvParseOutcome Parse(string csv) => CsvEntryParser.Parse(new StringReader(csv));

    [Fact]
    public void Parses_address_and_optional_notes()
    {
        var outcome = Parse("Address,Notes\n123 Main St,Blue house\n456 Oak Ave,\n");

        Assert.Empty(outcome.Failures);
        Assert.Equal(2, outcome.Rows.Count);

        Assert.Equal("123 Main St", outcome.Rows[0].Address);
        Assert.Equal("Blue house", outcome.Rows[0].Notes);
        Assert.Equal(2, outcome.Rows[0].RowNumber); // header is row 1

        Assert.Equal("456 Oak Ave", outcome.Rows[1].Address);
        Assert.Null(outcome.Rows[1].Notes);
    }

    [Fact]
    public void Reports_rows_with_missing_address_as_parse_failures()
    {
        var outcome = Parse("Address,Notes\n123 Main St,ok\n,orphan note\n789 Pine St,ok\n");

        Assert.Equal(2, outcome.Rows.Count);
        var failure = Assert.Single(outcome.Failures);
        Assert.Equal(3, failure.RowNumber);
    }

    [Fact]
    public void Notes_column_is_optional()
    {
        var outcome = Parse("Address\n123 Main St\n456 Oak Ave\n");

        Assert.Equal(2, outcome.Rows.Count);
        Assert.All(outcome.Rows, r => Assert.Null(r.Notes));
    }

    [Fact]
    public void Missing_address_header_throws_format_exception()
    {
        Assert.Throws<CsvImportFormatException>(() => Parse("Street,Notes\n123 Main St,x\n"));
    }
}
