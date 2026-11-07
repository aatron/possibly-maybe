using System.Globalization;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace PossiblyMaybe.Cli.Import;

/// <summary>SBRO Wayback xlsx: Date(MDD) Rot VH Team … Open Close (total = larger Open/Close across the VH pair).</summary>
public static class SbroXlsxParser
{
    static readonly XNamespace Ss = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    static readonly Regex SeasonFile = new(@"(\d{4})-(\d{2})", RegexOptions.Compiled);

    public sealed record GameRow(
        DateOnly Date,
        string Away,
        string Home,
        int? AwayScore,
        int? HomeScore,
        double OpenTotal,
        double CloseTotal);

    public static (int Year1, int Year2) SeasonYearsFromPath(string path)
    {
        var m = SeasonFile.Match(Path.GetFileName(path));
        if (!m.Success) throw new InvalidOperationException($"no YYYY-YY season in {path}");
        var y1 = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        var yy = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
        var y2 = y1 / 100 * 100 + yy;
        if (y2 < y1) y2 += 100;
        return (y1, y2);
    }

    public static DateOnly ParseMdd(string mddRaw, int year1, int year2)
    {
        var s = mddRaw.Trim();
        if (s.Contains('.', StringComparison.Ordinal)) s = s.Split('.')[0];
        if (!int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) || n <= 0)
            throw new FormatException($"bad MDD '{mddRaw}'");
        int month, day;
        if (n <= 999)
        {
            month = n / 100;
            day = n % 100;
        }
        else
        {
            month = n / 100;
            day = n % 100;
        }
        var year = month >= 8 ? year1 : year2;
        return new DateOnly(year, month, day);
    }

    /// <summary>Per side, total is the larger numeric Open/Close across the two VH rows (spread is the small one).</summary>
    public static bool TryPickTotals(
        string openA, string closeA, string openB, string closeB,
        out double openTotal, out double closeTotal)
    {
        openTotal = closeTotal = 0;
        var o = MaxLine(openA, openB);
        var c = MaxLine(closeA, closeB);
        if (o is null || c is null) return false;
        // Spreads are the small number; reject when the "max" is still spread-sized.
        if (o.Value < 15 || c.Value < 15) return false;
        openTotal = o.Value;
        closeTotal = c.Value;
        return true;
    }

    public static IEnumerable<GameRow> ParseFile(string path)
    {
        var (y1, y2) = SeasonYearsFromPath(path);
        var rows = ReadSheetRows(path);
        if (rows.Count < 3) yield break;

        var header = rows[0];
        var iDate = Col(header, "Date");
        var iVh = Col(header, "VH");
        var iTeam = Col(header, "Team");
        var iFinal = Col(header, "Final");
        var iOpen = Col(header, "Open");
        var iClose = Col(header, "Close");

        for (var i = 1; i + 1 < rows.Count; i += 2)
        {
            var a = rows[i];
            var b = rows[i + 1];
            if (a.Count <= iTeam || b.Count <= iTeam) continue;
            if (!LooksLikeMdd(Cell(a, iDate))) { i--; continue; }

            DateOnly date;
            try { date = ParseMdd(Cell(a, iDate), y1, y2); }
            catch { continue; }

            var (away, home) = AssignSides(Cell(a, iVh), Cell(a, iTeam), Cell(b, iVh), Cell(b, iTeam));
            if (string.IsNullOrWhiteSpace(away) || string.IsNullOrWhiteSpace(home)) continue;
            if (!TryPickTotals(Cell(a, iOpen), Cell(a, iClose), Cell(b, iOpen), Cell(b, iClose), out var ot, out var ct))
                continue;

            yield return new GameRow(
                date, away.Trim(), home.Trim(),
                ParseScore(Cell(a, iFinal)), ParseScore(Cell(b, iFinal)),
                ot, ct);
        }
    }

    static (string Away, string Home) AssignSides(string vhA, string teamA, string vhB, string teamB)
    {
        // V=away, H=home; N/N → first row away, second home
        if (Eq(vhA, "V") || Eq(vhA, "N"))
            return (teamA, teamB);
        if (Eq(vhA, "H"))
            return (teamB, teamA);
        return (teamA, teamB);
    }

    static bool Eq(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    static bool LooksLikeMdd(string s)
        => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && d is > 0 and < 1300;

    static double? MaxLine(string a, string b)
    {
        var va = ParseLine(a);
        var vb = ParseLine(b);
        if (va is null) return vb;
        if (vb is null) return va;
        return Math.Max(va.Value, vb.Value);
    }

    static double? ParseLine(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var t = raw.Trim();
        if (t.Equals("pk", StringComparison.OrdinalIgnoreCase) ||
            t.Equals("NL", StringComparison.OrdinalIgnoreCase) ||
            t.Equals("nl", StringComparison.OrdinalIgnoreCase))
            return null;
        return double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    static int? ParseScore(string raw)
        => int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null;

    static string Cell(IReadOnlyList<string> row, int i) => i < row.Count ? row[i] : "";

    static int Col(IReadOnlyList<string> header, string name)
    {
        for (var i = 0; i < header.Count; i++)
            if (string.Equals(header[i].Trim(), name, StringComparison.OrdinalIgnoreCase))
                return i;
        throw new InvalidOperationException($"column '{name}' missing");
    }

    public static List<List<string>> ReadSheetRows(string path)
    {
        using var zip = ZipFile.OpenRead(path);
        var shared = ReadShared(zip);
        var sheet = zip.GetEntry("xl/worksheets/sheet1.xml")
            ?? throw new InvalidOperationException($"no sheet1 in {path}");
        using var stream = sheet.Open();
        var doc = XDocument.Load(stream);
        var rows = new List<List<string>>();
        foreach (var row in doc.Root!.Element(Ss + "sheetData")!.Elements(Ss + "row"))
        {
            var map = new Dictionary<int, string>();
            var max = -1;
            foreach (var c in row.Elements(Ss + "c"))
            {
                var refAttr = (string?)c.Attribute("r") ?? "";
                var col = ColIndex(refAttr);
                max = Math.Max(max, col);
                var t = (string?)c.Attribute("t");
                var v = c.Element(Ss + "v")?.Value ?? "";
                if (t == "s" && int.TryParse(v, out var si) && si >= 0 && si < shared.Count)
                    v = shared[si];
                map[col] = v;
            }
            if (max < 0) continue;
            var list = new List<string>(max + 1);
            for (var i = 0; i <= max; i++)
                list.Add(map.TryGetValue(i, out var s) ? s : "");
            rows.Add(list);
        }
        return rows;
    }

    static List<string> ReadShared(ZipArchive zip)
    {
        var e = zip.GetEntry("xl/sharedStrings.xml");
        if (e is null) return [];
        using var stream = e.Open();
        var doc = XDocument.Load(stream);
        var list = new List<string>();
        foreach (var si in doc.Root!.Elements(Ss + "si"))
        {
            var texts = si.Descendants(Ss + "t").Select(t => t.Value);
            list.Add(string.Concat(texts));
        }
        return list;
    }

    static int ColIndex(string cellRef)
    {
        var n = 0;
        foreach (var ch in cellRef)
        {
            if (!char.IsLetter(ch)) break;
            n = n * 26 + (char.ToUpperInvariant(ch) - 'A' + 1);
        }
        return n - 1;
    }
}
