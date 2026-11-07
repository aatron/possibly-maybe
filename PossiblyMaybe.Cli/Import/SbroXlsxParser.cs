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
    /// <summary>NHL covid short season file <c>nhl_odds_2021.xlsx</c> (= 2020-21).</summary>
    static readonly Regex NhlSingleYearFile = new(@"nhl_odds_(\d{4})\.xlsx$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public sealed record GameRow(
        DateOnly Date,
        string Away,
        string Home,
        int? AwayScore,
        int? HomeScore,
        double OpenTotal,
        double CloseTotal);

    /// <summary>NHL SBRO row: totals live in OpenOU/CloseOU with real over juice (not ML Open/Close).</summary>
    public sealed record NhlGameRow(
        DateOnly Date,
        string Away,
        string Home,
        int? AwayScore,
        int? HomeScore,
        double OpenTotal,
        double CloseTotal,
        int OpenOverAmerican,
        int CloseOverAmerican,
        bool OpenJuiceAssumed,
        bool CloseJuiceAssumed);

    public static (int Year1, int Year2) SeasonYearsFromPath(string path)
    {
        var name = Path.GetFileName(path);
        var m = SeasonFile.Match(name);
        if (m.Success)
        {
            var y1 = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            var yy = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
            var y2 = y1 / 100 * 100 + yy;
            if (y2 < y1) y2 += 100;
            return (y1, y2);
        }

        var single = NhlSingleYearFile.Match(name);
        if (single.Success)
        {
            // nhl_odds_2021.xlsx is the 2020-21 short season (starts Jan).
            var y2 = int.Parse(single.Groups[1].Value, CultureInfo.InvariantCulture);
            return (y2 - 1, y2);
        }

        throw new InvalidOperationException($"no YYYY-YY (or nhl_odds_YYYY) season in {path}");
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

    /// <summary>NHL OpenOU/CloseOU are goal totals (~5–7); reject NL/pk/empty.</summary>
    public static bool TryPickNhlOu(
        string openAway, string openHome, string closeAway, string closeHome,
        out double openTotal, out double closeTotal)
    {
        openTotal = closeTotal = 0;
        var o = FirstLine(openAway, openHome);
        var c = FirstLine(closeAway, closeHome);
        if (o is null || c is null) return false;
        // NHL regulation totals sit well below basketball; reject ML-sized leftovers.
        if (o.Value is < 3 or > 12 || c.Value is < 3 or > 12) return false;
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

    /// <summary>
    /// NHL SBRO: Open/Close are moneylines; totals + juice are OpenOU/(blank)/CloseOU/(blank).
    /// Optional PuckLine column sits between Close and OpenOU on later seasons.
    /// Away-row juice is treated as the over price (home-row = under).
    /// </summary>
    public static IEnumerable<NhlGameRow> ParseNhlFile(string path)
    {
        var (y1, y2) = SeasonYearsFromPath(path);
        var rows = ReadSheetRows(path);
        if (rows.Count < 3) yield break;

        var header = rows[0];
        var iDate = Col(header, "Date");
        var iVh = Col(header, "VH");
        var iTeam = Col(header, "Team");
        var iFinal = Col(header, "Final");
        var iOpenOu = Col(header, "OpenOU");
        var iCloseOu = Col(header, "CloseOU");
        // Juice lives in the unlabeled column immediately after each OU header.
        var iOpenJuice = iOpenOu + 1;
        var iCloseJuice = iCloseOu + 1;

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

            // Align OU cells to V/H after AssignSides (a may be home).
            string openV, openH, closeV, closeH, juiceOpenV, juiceCloseV;
            if (Eq(Cell(a, iVh), "H"))
            {
                openV = Cell(b, iOpenOu); openH = Cell(a, iOpenOu);
                closeV = Cell(b, iCloseOu); closeH = Cell(a, iCloseOu);
                juiceOpenV = Cell(b, iOpenJuice); juiceCloseV = Cell(b, iCloseJuice);
            }
            else
            {
                openV = Cell(a, iOpenOu); openH = Cell(b, iOpenOu);
                closeV = Cell(a, iCloseOu); closeH = Cell(b, iCloseOu);
                juiceOpenV = Cell(a, iOpenJuice); juiceCloseV = Cell(a, iCloseJuice);
            }

            if (!TryPickNhlOu(openV, openH, closeV, closeH, out var ot, out var ct))
                continue;

            var (openAm, openAssumed) = ParseAmericanOrAssume(juiceOpenV);
            var (closeAm, closeAssumed) = ParseAmericanOrAssume(juiceCloseV);

            int? awayScore, homeScore;
            if (Eq(Cell(a, iVh), "H"))
            {
                awayScore = ParseScore(Cell(b, iFinal));
                homeScore = ParseScore(Cell(a, iFinal));
            }
            else
            {
                awayScore = ParseScore(Cell(a, iFinal));
                homeScore = ParseScore(Cell(b, iFinal));
            }

            yield return new NhlGameRow(
                date, away.Trim(), home.Trim(),
                awayScore, homeScore,
                ot, ct, openAm, closeAm, openAssumed, closeAssumed);
        }
    }

    static (int American, bool Assumed) ParseAmericanOrAssume(string raw)
    {
        if (int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n != 0)
            return (n, false);
        return (-110, true);
    }

    static double? FirstLine(string a, string b)
    {
        var va = ParseLine(a);
        if (va is not null) return va;
        return ParseLine(b);
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
