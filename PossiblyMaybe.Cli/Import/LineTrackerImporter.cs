using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PossiblyMaybe.Data;
using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Cli.Import;

/// <summary>
/// Maps data/line_snapshots.db (scripts/line_tracker.py) into warehouse OddsSnapshots.
/// Open = earliest capture (ESPN prefers reported open_line); close = last with minutes_to_start &gt; 0.
/// Never touches paper.db.
/// </summary>
public static class LineTrackerImporter
{
    static readonly Regex KeyRe = new(
        @"^(?<ymd>\d{8}) (?<away>\w+)@(?<home>\w+)(?: (?<hhmm>\d{4}))?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static async Task ImportAsync(
        string snapshotsDbPath, string warehouseDbPath, CancellationToken ct = default)
    {
        if (!File.Exists(snapshotsDbPath))
        {
            Console.WriteLine($"line-tracker: no snapshots DB at {snapshotsDbPath}");
            Console.WriteLine("line-tracker coverage: 0/0 games (n/a) have open+close");
            return;
        }

        var raw = ReadSnapshots(snapshotsDbPath);
        var groups = raw
            .GroupBy(r => (r.Sport, r.GameKey, r.StartUtc, r.Venue))
            .ToList();
        var allGames = raw
            .Select(r => (r.Sport, r.GameKey, r.StartUtc))
            .ToHashSet();
        var gamesWithBoth = new HashSet<(string Sport, string GameKey, DateTimeOffset StartUtc)>();

        var cs = $"Data Source={warehouseDbPath}";
        await AppDbContext.EnsureCreatedAsync(cs, ct);
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(cs).Options;
        await using var db = new AppDbContext(options);

        var sports = groups.Select(g => g.Key.Sport).Distinct().ToList();
        var games = await db.Games.Where(g => sports.Contains(g.Sport)).ToListAsync(ct);

        var gameIdsTouched = new HashSet<int>();
        var booksWritten = new HashSet<Book>();
        var snaps = new List<OddsSnapshot>();

        foreach (var grp in groups)
        {
            var book = MapBook(grp.Key.Venue);
            if (book is null) continue;

            var oc = ResolveOpenClose(grp.ToList(), preferEspnOpenLine: grp.Key.Venue == "espn");
            if (oc.OpenLine is null && oc.CloseLine is null) continue;

            if (!TryParseTeams(grp.Key.GameKey, out var away, out var home, out var etDate))
                continue;

            var g = FindOrCreateGame(db, games, grp.Key.Sport, home, away, etDate, grp.Key.StartUtc);
            gameIdsTouched.Add(g.Id);
            booksWritten.Add(book.Value);

            if (oc.OpenLine is { } ol)
            {
                snaps.Add(TotalSnap(g.Id, book.Value, ol, oc.OpenAmerican ?? -110,
                    PriceKind.Open, oc.OpenAt ?? grp.Key.StartUtc.AddDays(-7),
                    assumed: oc.OpenAmerican is null));
            }

            if (oc.CloseLine is { } cl)
            {
                snaps.Add(TotalSnap(g.Id, book.Value, cl, oc.CloseAmerican ?? -110,
                    PriceKind.Close, oc.CloseAt ?? grp.Key.StartUtc,
                    assumed: oc.CloseAmerican is null));
            }

            if (oc.OpenLine is not null && oc.CloseLine is not null)
                gamesWithBoth.Add((grp.Key.Sport, grp.Key.GameKey, grp.Key.StartUtc));
        }

        if (gameIdsTouched.Count > 0 && booksWritten.Count > 0)
        {
            var ids = gameIdsTouched.ToList();
            var books = booksWritten.ToList();
            await db.OddsSnapshots
                .Where(o => ids.Contains(o.GameId) && books.Contains(o.Book) && o.Market == "TOTAL")
                .ExecuteDeleteAsync(ct);
        }

        db.OddsSnapshots.AddRange(snaps);
        await db.SaveChangesAsync(ct);

        var nGames = allGames.Count;
        var nBoth = gamesWithBoth.Count;
        var pct = nGames == 0 ? 0.0 : 100.0 * nBoth / nGames;
        Console.WriteLine(
            $"line-tracker import: snaps={snaps.Count} games={nGames} venue-groups={groups.Count}");
        Console.WriteLine(
            $"line-tracker coverage: {nBoth}/{nGames} games ({pct:0.#}%) have open+close");
    }

    /// <summary>Open = earliest capture; close = last with minutes_to_start &gt; 0. ESPN may use open_line.</summary>
    public static OpenClose ResolveOpenClose(IReadOnlyList<SnapRow> rows, bool preferEspnOpenLine)
    {
        if (rows.Count == 0) return default;
        var ordered = rows.OrderBy(r => r.CapturedAt).ThenBy(r => r.Line).ToList();
        var byCapture = ordered.GroupBy(r => r.CapturedAt).OrderBy(g => g.Key).ToList();

        double? openLine = null;
        int? openAm = null;
        DateTimeOffset? openAt = null;
        if (preferEspnOpenLine)
        {
            var withOpen = ordered.FirstOrDefault(r => r.OpenLine is not null);
            if (withOpen.OpenLine is { } espnOpen)
            {
                openLine = espnOpen;
                openAm = withOpen.OverAmerican ?? -110;
                openAt = byCapture[0].Key;
            }
        }

        if (openLine is null)
        {
            var first = PickQuote(byCapture[0].ToList());
            if (first is { } f)
            {
                openLine = f.Line;
                openAm = f.American;
                openAt = byCapture[0].Key;
            }
        }

        double? closeLine = null;
        int? closeAm = null;
        DateTimeOffset? closeAt = null;
        for (var i = byCapture.Count - 1; i >= 0; i--)
        {
            var cap = byCapture[i];
            if (cap.Any(r => r.MinutesToStart is null or <= 0)) continue;
            var q = PickQuote(cap.ToList());
            if (q is null) continue;
            closeLine = q.Value.Line;
            closeAm = q.Value.American;
            closeAt = cap.Key;
            break;
        }

        return new OpenClose(openLine, openAt, openAm, closeLine, closeAt, closeAm);
    }

    public static (double Line, int American)? PickQuote(IReadOnlyList<SnapRow> atCapture)
    {
        if (atCapture.Count == 0) return null;
        if (atCapture.Count == 1)
        {
            var r = atCapture[0];
            var am = r.OverAmerican
                     ?? (r.OverAsk is { } ask && ask is > 0 and < 1
                         ? OddsMath.AmericanFromBuyPrice(ask)
                         : -110);
            return (r.Line, am);
        }

        // prediction-market ladder: strike where mid P(over)≈0.5, American from over ask
        var pts = atCapture
            .Where(r => r.OverAsk is > 0 and < 1 && r.UnderAsk is > 0 and < 1
                        && r.OverAsk.Value + r.UnderAsk.Value <= 1.10)
            .Select(r => (r.Line, Mid: (r.OverAsk!.Value + 1 - r.UnderAsk!.Value) / 2, Ask: r.OverAsk!.Value))
            .OrderBy(p => p.Line)
            .ToList();
        if (pts.Count == 0)
        {
            var r = atCapture.OrderBy(x => x.Line).First();
            return (r.Line, r.OverAmerican ?? -110);
        }

        for (var i = 0; i < pts.Count - 1; i++)
        {
            var (k0, p0, a0) = pts[i];
            var (k1, p1, a1) = pts[i + 1];
            if (p0 >= 0.5 && p1 <= 0.5 && Math.Abs(p0 - p1) > 1e-9)
            {
                var line = Math.Round(k0 + (k1 - k0) * (p0 - 0.5) / (p0 - p1), 2);
                var ask = Math.Abs(p0 - 0.5) <= Math.Abs(p1 - 0.5) ? a0 : a1;
                return (line, OddsMath.AmericanFromBuyPrice(ask));
            }
        }

        var nearest = pts.OrderBy(p => Math.Abs(p.Mid - 0.5)).First();
        return (nearest.Line, OddsMath.AmericanFromBuyPrice(nearest.Ask));
    }

    static Book? MapBook(string venue) => venue.ToLowerInvariant() switch
    {
        "espn" => Book.DraftKings,
        "kalshi" => Book.Kalshi,
        "cryptocom" => Book.CryptoCom,
        _ => null,
    };

    static bool TryParseTeams(string gameKey, out string away, out string home, out DateOnly etDate)
    {
        away = home = "";
        etDate = default;
        var m = KeyRe.Match(gameKey);
        if (!m.Success) return false;
        away = m.Groups["away"].Value;
        home = m.Groups["home"].Value;
        var ymd = m.Groups["ymd"].Value;
        etDate = new DateOnly(
            int.Parse(ymd[..4], CultureInfo.InvariantCulture),
            int.Parse(ymd[4..6], CultureInfo.InvariantCulture),
            int.Parse(ymd[6..8], CultureInfo.InvariantCulture));
        return true;
    }

    static Game FindOrCreateGame(
        AppDbContext db, List<Game> games, string sport, string home, string away,
        DateOnly etDate, DateTimeOffset startUtc)
    {
        // Prefer tipoff proximity so doubleheaders on the same SourceKey date still split.
        var hit = games
            .Where(g => g.Sport.Equals(sport, StringComparison.OrdinalIgnoreCase)
                        && g.HomeTeam == home && g.AwayTeam == away
                        && Math.Abs((g.TipoffUtc - startUtc).TotalHours) < 14)
            .OrderBy(g => Math.Abs((g.TipoffUtc - startUtc).TotalMinutes))
            .FirstOrDefault();
        if (hit is not null) return hit;

        var key = SportTeams.GameKey(sport.ToLowerInvariant(), etDate, home, away);
        // Doubleheader: append UTC HHMM when date-only SourceKey already taken.
        if (games.Any(g => g.SourceKey == key))
            key = $"{key}:{startUtc.UtcDateTime:HHmm}";

        var g = new Game
        {
            Sport = sport.ToUpperInvariant(),
            Season = etDate.Year.ToString(CultureInfo.InvariantCulture),
            SourceKey = key,
            TipoffUtc = startUtc,
            HomeTeam = home,
            AwayTeam = away,
        };
        db.Games.Add(g);
        db.SaveChanges();
        games.Add(g);
        return g;
    }

    static OddsSnapshot TotalSnap(
        int gameId, Book book, double line, int american, PriceKind kind, DateTimeOffset at, bool assumed)
        => new()
        {
            GameId = gameId,
            Book = book,
            Market = "TOTAL",
            Side = "over",
            Line = line,
            AmericanOdds = american,
            PriceKind = kind,
            ClvQuality = ClvQuality.OpenClose,
            AssumedJuice = assumed,
            CapturedAt = at,
        };

    static List<SnapRow> ReadSnapshots(string path)
    {
        var list = new List<SnapRow>();
        using var con = new SqliteConnection($"Data Source={path}");
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = """
            SELECT sport, game_key, start_utc, captured_at, minutes_to_start, venue, source,
                   line, over_ask, under_ask, over_american, under_american, open_line
            FROM line_snapshots
            """;
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new SnapRow(
                Sport: r.GetString(0),
                GameKey: r.GetString(1),
                StartUtc: DateTimeOffset.Parse(r.GetString(2), CultureInfo.InvariantCulture),
                CapturedAt: DateTimeOffset.Parse(r.GetString(3), CultureInfo.InvariantCulture),
                MinutesToStart: r.IsDBNull(4) ? null : r.GetDouble(4),
                Venue: r.GetString(5),
                Source: r.IsDBNull(6) ? null : r.GetString(6),
                Line: r.GetDouble(7),
                OverAsk: r.IsDBNull(8) ? null : r.GetDouble(8),
                UnderAsk: r.IsDBNull(9) ? null : r.GetDouble(9),
                OverAmerican: r.IsDBNull(10) ? null : r.GetInt32(10),
                UnderAmerican: r.IsDBNull(11) ? null : r.GetInt32(11),
                OpenLine: r.IsDBNull(12) ? null : r.GetDouble(12)));
        }

        return list;
    }

    public readonly record struct SnapRow(
        string Sport, string GameKey, DateTimeOffset StartUtc, DateTimeOffset CapturedAt,
        double? MinutesToStart, string Venue, string? Source, double Line,
        double? OverAsk, double? UnderAsk, int? OverAmerican, int? UnderAmerican, double? OpenLine);

    public readonly record struct OpenClose(
        double? OpenLine, DateTimeOffset? OpenAt, int? OpenAmerican,
        double? CloseLine, DateTimeOffset? CloseAt, int? CloseAmerican);
}
