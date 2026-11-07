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
        var unmatched = 0;

        foreach (var grp in groups)
        {
            var book = MapBook(grp.Key.Venue);
            if (book is null) continue;

            var oc = ResolveOpenClose(grp.ToList(), preferEspnOpenLine: grp.Key.Venue == "espn");
            if (oc.OpenLine is null && oc.CloseLine is null) continue;

            if (!TryParseTeams(grp.Key.GameKey, out var awayRaw, out var homeRaw, out _))
                continue;

            // Match warehouse games only — never invent GameIds (A2).
            var g = FindGame(games, grp.Key.Sport, homeRaw, awayRaw, grp.Key.StartUtc);
            if (g is null)
            {
                unmatched++;
                continue;
            }

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
            $"line-tracker import: snaps={snaps.Count} games={nGames} venue-groups={groups.Count} unmatched={unmatched}");
        Console.WriteLine(
            $"line-tracker coverage: {nBoth}/{nGames} games ({pct:0.#}%) have open+close");
        foreach (var sport in sports.OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
        {
            var sportGames = allGames.Where(g => g.Sport.Equals(sport, StringComparison.OrdinalIgnoreCase)).ToList();
            var sportBoth = gamesWithBoth.Count(g => g.Sport.Equals(sport, StringComparison.OrdinalIgnoreCase));
            var sp = sportGames.Count == 0 ? 0.0 : 100.0 * sportBoth / sportGames.Count;
            Console.WriteLine($"  {sport}: {sportBoth}/{sportGames.Count} ({sp:0.#}%) open+close");
        }
    }

    /// <summary>
    /// Last pre-start total from line_snapshots.db for a paper game key (sport + yyyymmdd away@home).
    /// When <paramref name="trackerVenue"/> is set (espn/kalshi/cryptocom), only that venue is used (R3).
    /// </summary>
    public static (double Line, int American, double? MinutesToStart)? TryCloseFromSnapshots(
        string snapshotsDbPath, string sport, string gameKey, string? trackerVenue = null)
    {
        if (!File.Exists(snapshotsDbPath)) return null;
        var raw = ReadSnapshots(snapshotsDbPath)
            .Where(r => (string.IsNullOrEmpty(sport)
                         || r.Sport.Equals(sport, StringComparison.OrdinalIgnoreCase))
                        && (r.GameKey.Equals(gameKey, StringComparison.OrdinalIgnoreCase)
                            || r.GameKey.StartsWith(gameKey + " ", StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (!string.IsNullOrEmpty(trackerVenue))
            raw = raw.Where(r => r.Venue.Equals(trackerVenue, StringComparison.OrdinalIgnoreCase)).ToList();
        if (raw.Count == 0) return null;
        var preferEspn = raw.Any(r => r.Venue.Equals("espn", StringComparison.OrdinalIgnoreCase));
        var oc = ResolveOpenClose(raw, preferEspnOpenLine: preferEspn);
        if (oc.CloseLine is not { } cl) return null;
        double? mins = null;
        if (oc.CloseAt is { } closeAt)
        {
            var start = raw.Select(r => r.StartUtc).DefaultIfEmpty().Max();
            if (start != default)
                mins = (start - closeAt).TotalMinutes;
        }

        return (cl, oc.CloseAmerican ?? -110, mins);
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

    /// <summary>Map tracker abbr → warehouse team string; unmapped keeps own code (never another club).</summary>
    public static string ResolveTeam(string sport, string code) => sport.ToUpperInvariant() switch
    {
        "NFL" => SportTeams.TryNfl(code) ?? code,
        "MLB" => SportTeams.TryMlb(code) ?? code,
        "NBA" => TeamCodes.TryNormalize(code) ?? code,
        "MLS" => SportTeams.TryMls(code) ?? code,
        "LIGUE1" => SportTeams.TryLigue1(code) ?? code,
        "SERIEA" => SportTeams.TrySerieA(code) ?? code,
        "LIGAMX" => SportTeams.TryLigaMx(code) ?? code,
        "NCAAB" or "NCAAF" => SportTeams.NcaaCode(code),
        _ => code,
    };

    /// <summary>Match an existing warehouse game by sport + teams + tipoff; never creates rows.</summary>
    public static Game? FindGame(
        List<Game> games, string sport, string homeRaw, string awayRaw, DateTimeOffset startUtc)
    {
        var home = ResolveTeam(sport, homeRaw);
        var away = ResolveTeam(sport, awayRaw);
        // Prefer tipoff proximity so doubleheaders on the same date still split.
        var hit = games
            .Where(g => g.Sport.Equals(sport, StringComparison.OrdinalIgnoreCase)
                        && TeamEquals(g.HomeTeam, home, homeRaw)
                        && TeamEquals(g.AwayTeam, away, awayRaw)
                        && Math.Abs((g.TipoffUtc - startUtc).TotalHours) < 14)
            .OrderBy(g => Math.Abs((g.TipoffUtc - startUtc).TotalMinutes))
            .FirstOrDefault();
        if (hit is not null) return hit;

        // Soccer warehouse keys use UTC calendar date; tracker game_key uses ET — allow ±1 day on SourceKey date.
        return games
            .Where(g => g.Sport.Equals(sport, StringComparison.OrdinalIgnoreCase)
                        && TeamEquals(g.HomeTeam, home, homeRaw)
                        && TeamEquals(g.AwayTeam, away, awayRaw)
                        && Math.Abs((g.TipoffUtc.UtcDateTime.Date - startUtc.UtcDateTime.Date).TotalDays) <= 1)
            .OrderBy(g => Math.Abs((g.TipoffUtc - startUtc).TotalMinutes))
            .FirstOrDefault();
    }

    static bool TeamEquals(string warehouse, string resolved, string raw)
        => warehouse.Equals(resolved, StringComparison.OrdinalIgnoreCase)
           || warehouse.Equals(raw, StringComparison.OrdinalIgnoreCase);

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
