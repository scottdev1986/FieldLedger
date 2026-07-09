using FieldLedger.Api;

namespace FieldLedger.Api.Tests;

public sealed class CsvFormatterTests
{
    [Fact]
    public void FormatRow_UsesInvariantValuesAndEscapesCsvCharacters()
    {
        var result = CsvFormatter.FormatRow(
            "Cedar, Lane", "quoted \"value\"", "line\nbreak", 1234.50m,
            new DateOnly(2026, 7, 9), null);

        Assert.Equal(
            "\"Cedar, Lane\",\"quoted \"\"value\"\"\",\"line\nbreak\",1234.50,2026-07-09,",
            result);
    }
}
