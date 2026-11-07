using System.Globalization;
using PossiblyMaybe.Data;
using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Cli.Import;

/// <summary>
/// Aussportsbetting NFL xlsx (archive.org): <c>Total Score Open/Close</c> plus over/under open/close decimal odds.
/// Skips serial-1900 junk dates and rows without both totals.
/// </summary>
public static class AusNflXlsxParser
{
    /// <summary>Excel/OLE serial epoch (1899-12-30) used by the archived sheet.</summary>
    static readonly DateOnly ExcelEpoch = new(1899, 12, 30);

    public sealed record Row(
        DateOnly Date,
        string HomeRaw,
        string AwayRaw,
        int? HomeScore,
        int? AwayScore,
        double OpenTotal,
        double CloseTotal,
        double OverOpenDec,
        double OverCloseDec,
        double UnderOpenDec,
        double UnderCloseDec);

    public static IEnumerable<Row> ParseFile(string path)
    {
        var rows = SbroXlsxParser.ReadSheetRows(path);
        if (rows.Count < 2) yield break;

        var header = rows[0];
        var iDate = Col(header, "Date");
        var iHome = Col(header, "Home Team");
        var iAway = Col(header, "Away Team");
        var iHs = Col(header, "Home Score");
        var iAs = Col(header, "Away Score");
        var iOpen = Col(header, "Total Score Open");
        var iClose = Col(header, "Total Score Close");
        var iOverOpen = Col(header, "Total Score Over Open");
        var iOverClose = Col(header, "Total Score Over Close");
        var iUnderOpen = Col(header, "Total Score Under Open");
        var iUnderClose = Col(header, "Total Score Under Close");

        for (var r = 1; r < rows.Count; r++)
        {
            if (TryParseRow(
                    rows[r], iDate, iHome, iAway, iHs, iAs,
                    iOpen, iClose, iOverOpen, iOverClose, iUnderOpen, iUnderClose,
                    out var row))
                yield return row;
        }
    }

    /// <summary>True for Excel serial junk that lands in 1900/1901 (or unparseable).</summary>
    public static bool IsJunkDate(DateOnly date) => date.Year is <= 1901;

    public static bool TryParseExcelSerial(string raw, out DateOnly date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(raw)) return false;
        var t = raw.Trim();
        if (DateOnly.TryParse(t, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            return !IsJunkDate(date);
        if (!double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var serial))
            return false;
        // Reject tiny / negative serials that become 1900–1901 junk.
        if (serial is < 2 or > 100_000) return false;
        date = ExcelEpoch.AddDays((int)serial);
        return !IsJunkDate(date);
    }

    public static bool TryParseRow(
        IReadOnlyList<string> cells,
        int iDate, int iHome, int iAway, int iHs, int iAs,
        int iOpen, int iClose, int iOverOpen, int iOverClose, int iUnderOpen, int iUnderClose,
        out Row row)
    {
        row = null!;
        if (!TryParseExcelSerial(Cell(cells, iDate), out var date)) return false;
        var home = Cell(cells, iHome).Trim();
        var away = Cell(cells, iAway).Trim();
        if (home.Length == 0 || away.Length == 0) return false;
        if (!TryDouble(Cell(cells, iOpen), out var openT) || !TryDouble(Cell(cells, iClose), out var closeT))
            return false;
        // NFL game totals are never tiny; reject empty/garbage that slipped through.
        if (openT < 20 || closeT < 20) return false;
        if (!TryDouble(Cell(cells, iOverOpen), out var overOpen) ||
            !TryDouble(Cell(cells, iOverClose), out var overClose) ||
            !TryDouble(Cell(cells, iUnderOpen), out var underOpen) ||
            !TryDouble(Cell(cells, iUnderClose), out var underClose))
            return false;
        if (overOpen <= 1 || overClose <= 1 || underOpen <= 1 || underClose <= 1) return false;

        row = new Row(
            date, home, away,
            TryInt(Cell(cells, iHs)), TryInt(Cell(cells, iAs)),
            openT, closeT, overOpen, overClose, underOpen, underClose);
        return true;
    }

    /// <summary>Header-aware parse used by tests with a full header + data row.</summary>
    public static bool TryParseDataRow(IReadOnlyList<string> header, IReadOnlyList<string> cells, out Row row)
    {
        row = null!;
        try
        {
            return TryParseRow(
                cells,
                Col(header, "Date"), Col(header, "Home Team"), Col(header, "Away Team"),
                Col(header, "Home Score"), Col(header, "Away Score"),
                Col(header, "Total Score Open"), Col(header, "Total Score Close"),
                Col(header, "Total Score Over Open"), Col(header, "Total Score Over Close"),
                Col(header, "Total Score Under Open"), Col(header, "Total Score Under Close"),
                out row);
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    public static int OverAmerican(Row row, PriceKind kind)
        => OddsMath.DecimalToAmerican(kind == PriceKind.Open ? row.OverOpenDec : row.OverCloseDec);

    public static int UnderAmerican(Row row, PriceKind kind)
        => OddsMath.DecimalToAmerican(kind == PriceKind.Open ? row.UnderOpenDec : row.UnderCloseDec);

    static bool TryDouble(string raw, out double v)
        => double.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v);

    static int? TryInt(string raw)
        => int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null;

    static string Cell(IReadOnlyList<string> row, int i) => i >= 0 && i < row.Count ? row[i] : "";

    static int Col(IReadOnlyList<string> header, string name)
    {
        for (var i = 0; i < header.Count; i++)
            if (string.Equals(header[i].Trim(), name, StringComparison.OrdinalIgnoreCase))
                return i;
        throw new InvalidOperationException($"column '{name}' missing");
    }
}
