using System.IO.Compression;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PossiblyMaybe.Cli.Import;
using PossiblyMaybe.Data;
using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Tests;

public class AusNflOddsImporterTests
{
    static readonly string[] Header =
    [
        "Date", "Home Team", "Away Team", "Home Score", "Away Score",
        "Overtime?", "Playoff Game?", "Neutral Venue?",
        "Home Odds Open", "Home Odds Min", "Home Odds Max", "Home Odds Close",
        "Away Odds Open", "Away Odds Min", "Away Odds Max", "Away Odds Close",
        "Home Line Open", "Home Line Min", "Home Line Max", "Home Line Close",
        "Away Line Open", "Away Line Min", "Away Line Max", "Away Line Close",
        "Home Line Odds Open", "Home Line Odds Min", "Home Line Odds Max", "Home Line Odds Close",
        "Away Line Odds Open", "Away Line Odds Min", "Away Line Odds Max", "Away Line Odds Close",
        "Total Score Open", "Total Score Min", "Total Score Max", "Total Score Close",
        "Total Score Over Open", "Total Score Over Min", "Total Score Over Max", "Total Score Over Close",
        "Total Score Under Open", "Total Score Under Min", "Total Score Under Max", "Total Score Under Close",
        "Notes",
    ];

    [Test]
    public async Task TryParseExcelSerial_Rejects1900Junk_AcceptsGameDates()
    {
        // Excel serial 1 → 1900-01-01 junk
        await Assert.That(AusNflXlsxParser.TryParseExcelSerial("1", out _)).IsFalse();
        await Assert.That(AusNflXlsxParser.IsJunkDate(new DateOnly(1900, 1, 1))).IsTrue();

        // 2022-09-11 ≈ serial 44815
        await Assert.That(AusNflXlsxParser.TryParseExcelSerial("44815", out var d)).IsTrue();
        await Assert.That(d).IsEqualTo(new DateOnly(2022, 9, 11));
    }

    [Test]
    public async Task TryParseDataRow_RequiresTotals_AndMapsOdds()
    {
        var good = Cells(
            date: "44815", home: "Buffalo Bills", away: "Los Angeles Rams",
            hs: "31", as_: "10",
            openT: "51.5", closeT: "52",
            overOpen: "1.909", overClose: "1.87",
            underOpen: "1.909", underClose: "1.95");
        await Assert.That(AusNflXlsxParser.TryParseDataRow(Header, good, out var row)).IsTrue();
        await Assert.That(row.OpenTotal).IsEqualTo(51.5);
        await Assert.That(row.CloseTotal).IsEqualTo(52);
        await Assert.That(AusNflXlsxParser.OverAmerican(row, PriceKind.Open)).IsEqualTo(-110);
        await Assert.That(AusNflXlsxParser.OverAmerican(row, PriceKind.Close)).IsEqualTo(-115);

        var noTot = Cells(
            date: "44815", home: "Buffalo Bills", away: "Los Angeles Rams",
            hs: "31", as_: "10",
            openT: "", closeT: "",
            overOpen: "1.909", overClose: "1.909",
            underOpen: "1.909", underClose: "1.909");
        await Assert.That(AusNflXlsxParser.TryParseDataRow(Header, noTot, out _)).IsFalse();
    }

    [Test]
    public async Task OregonCheck_NflNames_NotExcluded()
    {
        await Assert.That(OregonExclusion.IsExcluded("NFL", "Buffalo Bills", "Los Angeles Rams")).IsFalse();
        await Assert.That(OregonExclusion.IsExcluded("NFL", "KC", "BUF")).IsFalse();
        // Cardinal rule still armed for Oregon names if they ever appeared
        await Assert.That(OregonExclusion.IsExcluded("NFL", "Portland", "SEA")).IsTrue();
    }

    [Test]
    public async Task AusFindGame_MatchesExisting_DoesNotInvent()
    {
        var tip = new DateTimeOffset(2022, 9, 11, 17, 0, 0, TimeSpan.Zero);
        var g = new Game
        {
            Id = 7, Sport = "NFL", Season = "2022",
            SourceKey = SportTeams.GameKey("nfl", new DateOnly(2022, 9, 11), "ATL", "NO"),
            TipoffUtc = tip, HomeTeam = "ATL", AwayTeam = "NO",
        };
        var byKey = new Dictionary<string, Game>(StringComparer.Ordinal) { [g.SourceKey] = g };
        var byTeams = new Dictionary<string, Game>(StringComparer.Ordinal)
        {
            [$"ATL|NO|2022-09-11"] = g,
        };

        var hit = AusNflOddsImporter.FindGame(byKey, byTeams, new DateOnly(2022, 9, 11), "ATL", "NO");
        await Assert.That(hit?.Id).IsEqualTo(7);
        var miss = AusNflOddsImporter.FindGame(byKey, byTeams, new DateOnly(2022, 9, 11), "KC", "BUF");
        await Assert.That(miss).IsNull();
    }

    [Test]
    public async Task Import_AppendsRealPrice_DoesNotOverwriteAssumed()
    {
        var rawRoot = Path.Combine(Path.GetTempPath(), $"pm-aus-raw-{Guid.NewGuid():N}");
        var dbPath = Path.Combine(Path.GetTempPath(), $"pm-aus-{Guid.NewGuid():N}.db");
        var ausDir = Path.Combine(rawRoot, "round3_odds", "archive_org", "aussportsbetting");
        Directory.CreateDirectory(ausDir);
        try
        {
            WriteMinimalXlsx(
                Path.Combine(ausDir, "nfl_test.xlsx"),
                Header,
                Cells(
                    date: "44815", home: "Atlanta Falcons", away: "New Orleans Saints",
                    hs: "26", as_: "27",
                    openT: "43.5", closeT: "44",
                    overOpen: "1.909", overClose: "1.87",
                    underOpen: "1.909", underClose: "1.95"));

            var cs = $"Data Source={dbPath}";
            await AppDbContext.EnsureCreatedAsync(cs);
            var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(cs).Options;
            await using (var db = new AppDbContext(options))
            {
                var tip = new DateTimeOffset(2022, 9, 11, 17, 0, 0, TimeSpan.Zero);
                var g = new Game
                {
                    Sport = "NFL", Season = "2022",
                    SourceKey = SportTeams.GameKey("nfl", new DateOnly(2022, 9, 11), "ATL", "NO"),
                    TipoffUtc = tip, HomeTeam = "ATL", AwayTeam = "NO",
                    HomeScore = 26, AwayScore = 27,
                };
                db.Games.Add(g);
                await db.SaveChangesAsync();
                db.OddsSnapshots.Add(new OddsSnapshot
                {
                    GameId = g.Id, Book = Book.Consensus, Market = "TOTAL", Side = "over",
                    Line = 43.0, AmericanOdds = -110, PriceKind = PriceKind.Close,
                    ClvQuality = ClvQuality.CloseOnly, AssumedJuice = true, CapturedAt = tip,
                });
                await db.SaveChangesAsync();
            }

            var result = await AusNflOddsImporter.ImportAsync(rawRoot, dbPath);
            await Assert.That(result.Matched).IsEqualTo(1);
            await Assert.That(result.OregonHits).IsEqualTo(0);
            await Assert.That(result.SnapsWritten).IsEqualTo(4);

            await using (var db = new AppDbContext(options))
            {
                var snaps = db.OddsSnapshots.OrderBy(o => o.Id).ToList();
                await Assert.That(snaps.Count).IsEqualTo(5); // 1 assumed + 4 real
                await Assert.That(snaps.Count(s => s.AssumedJuice)).IsEqualTo(1);
                await Assert.That(snaps.Count(s => !s.AssumedJuice)).IsEqualTo(4);
                await Assert.That(snaps.Any(s => s.AssumedJuice && s.AmericanOdds == -110 && s.Line == 43.0)).IsTrue();
                await Assert.That(snaps.Any(s => !s.AssumedJuice && s.PriceKind == PriceKind.Open && s.Side == "over")).IsTrue();
                await Assert.That(snaps.Any(s => !s.AssumedJuice && s.PriceKind == PriceKind.Close && s.Side == "under")).IsTrue();
            }

            // Idempotent second pass
            var again = await AusNflOddsImporter.ImportAsync(rawRoot, dbPath);
            await Assert.That(again.Matched).IsEqualTo(0);
            await Assert.That(again.SkippedHaveReal).IsEqualTo(1);
        }
        finally
        {
            try { Directory.Delete(rawRoot, true); } catch { /* ignore */ }
            try { File.Delete(dbPath); } catch { /* ignore */ }
        }
    }

    static List<string> Cells(
        string date, string home, string away, string hs, string as_,
        string openT, string closeT,
        string overOpen, string overClose, string underOpen, string underClose)
    {
        var c = Enumerable.Repeat("", Header.Length).ToList();
        c[0] = date; c[1] = home; c[2] = away; c[3] = hs; c[4] = as_;
        c[32] = openT; c[33] = openT; c[34] = closeT; c[35] = closeT;
        c[36] = overOpen; c[37] = overOpen; c[38] = overClose; c[39] = overClose;
        c[40] = underOpen; c[41] = underOpen; c[42] = underClose; c[43] = underClose;
        return c;
    }

    /// <summary>Minimal shared-string xlsx with one sheet (enough for SbroXlsxParser.ReadSheetRows).</summary>
    static void WriteMinimalXlsx(string path, IReadOnlyList<string> header, IReadOnlyList<string> data)
    {
        var shared = new List<string>();
        int Share(string s)
        {
            var i = shared.IndexOf(s);
            if (i >= 0) return i;
            shared.Add(s);
            return shared.Count - 1;
        }

        string CellXml(int col, int row, string value, bool forceString)
        {
            var r = $"{ColName(col)}{row}";
            if (!forceString && double.TryParse(value, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out _))
                return $"<c r=\"{r}\"><v>{value}</v></c>";
            var si = Share(value);
            return $"<c r=\"{r}\" t=\"s\"><v>{si}</v></c>";
        }

        static string ColName(int idx)
        {
            var n = idx + 1;
            var s = "";
            while (n > 0)
            {
                n--;
                s = (char)('A' + n % 26) + s;
                n /= 26;
            }
            return s;
        }

        var sheet = new StringBuilder();
        sheet.Append("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
        sheet.Append("""<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData>""");
        sheet.Append("<row r=\"1\">");
        for (var i = 0; i < header.Count; i++)
            sheet.Append(CellXml(i, 1, header[i], forceString: true));
        sheet.Append("</row><row r=\"2\">");
        for (var i = 0; i < data.Count; i++)
            sheet.Append(CellXml(i, 2, data[i], forceString: i is 1 or 2 or 44));
        sheet.Append("</row></sheetData></worksheet>");

        var ss = new StringBuilder();
        ss.Append("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
        ss.Append("""<sst xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">""");
        foreach (var s in shared)
            ss.Append($"<si><t>{System.Security.SecurityElement.Escape(s)}</t></si>");
        ss.Append("</sst>");

        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        void Entry(string name, string body)
        {
            var e = zip.CreateEntry(name);
            using var w = new StreamWriter(e.Open(), new UTF8Encoding(false));
            w.Write(body);
        }
        Entry("[Content_Types].xml",
            """<?xml version="1.0" encoding="UTF-8"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/><Override PartName="/xl/sharedStrings.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sharedStrings+xml"/></Types>""");
        Entry("_rels/.rels",
            """<?xml version="1.0" encoding="UTF-8"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="/xl/workbook.xml" Id="rId1"/></Relationships>""");
        Entry("xl/workbook.xml",
            """<?xml version="1.0" encoding="UTF-8"?><workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="Data" sheetId="1" r:id="rId1"/></sheets></workbook>""");
        Entry("xl/_rels/workbook.xml.rels",
            """<?xml version="1.0" encoding="UTF-8"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml" Id="rId1"/><Relationship Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/sharedStrings" Target="sharedStrings.xml" Id="rId2"/></Relationships>""");
        Entry("xl/sharedStrings.xml", ss.ToString());
        Entry("xl/worksheets/sheet1.xml", sheet.ToString());
    }
}
