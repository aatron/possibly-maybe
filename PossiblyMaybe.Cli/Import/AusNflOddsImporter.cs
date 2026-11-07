using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PossiblyMaybe.Data;
using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Cli.Import;

/// <summary>
/// Import Aussportsbetting NFL O→C totals (2014–2025) onto existing warehouse games.
/// Never duplicates games; never overwrites assumed −110 rows — real prices are appended.
/// Oregon exclusion checked on every source row (none expected in NFL).
/// </summary>
public static class AusNflOddsImporter
{
    public const int MinSeason = 2014;
    public const int MaxSeason = 2025;
    public const string SourceBookLabel = "Aussportsbetting";

    public sealed record Result(
        int Files,
        int Parsed,
        int InWindow,
        int Matched,
        int Unmatched,
        int SkippedHaveReal,
        int OregonHits,
        int SnapsWritten,
        IReadOnlyList<(string Season, int GamesWithOpenClose, int GamesWithRealPrice)> SeasonStats);

    public static async Task<Result> ImportAsync(string rawRoot, string dbPath, CancellationToken ct = default)
    {
        var dir = Path.Combine(rawRoot, "round3_odds", "archive_org", "aussportsbetting");
        if (!Directory.Exists(dir))
            throw new DirectoryNotFoundException($"missing AUS NFL folder: {dir}");

        var files = Directory.GetFiles(dir, "nfl_*.xlsx").OrderBy(x => x, StringComparer.Ordinal).ToArray();
        if (files.Length == 0)
            throw new FileNotFoundException($"no nfl_*.xlsx under {dir}");

        var cs = $"Data Source={dbPath}";
        await AppDbContext.EnsureCreatedAsync(cs, ct);
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(cs).Options;
        await using var db = new AppDbContext(options);

        var games = await db.Games.Where(g => g.Sport == "NFL").ToListAsync(ct);
        var byKey = games.ToDictionary(g => g.SourceKey, StringComparer.Ordinal);
        // Secondary index: home|away|calendar-date → game (for tipoff-date drift)
        var byTeamsDate = games
            .GroupBy(g => TeamDateKey(g.HomeTeam, g.AwayTeam, DateOnly.FromDateTime(g.TipoffUtc.UtcDateTime)))
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        var haveReal = await GamesWithRealAusAsync(db, ct);

        var pending = new List<OddsSnapshot>();
        var matchedIds = new HashSet<int>();
        var parsed = 0;
        var inWindow = 0;
        var matched = 0;
        var unmatched = 0;
        var skippedHave = 0;
        var oregon = 0;
        var unmapped = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            foreach (var row in AusNflXlsxParser.ParseFile(file))
            {
                parsed++;
                // Cardinal rule on every source row (raw names + mapped codes).
                if (OregonExclusion.IsExcluded("NFL", row.HomeRaw, row.AwayRaw))
                {
                    oregon++;
                    continue;
                }

                var home = SportTeams.TryNfl(row.HomeRaw);
                var away = SportTeams.TryNfl(row.AwayRaw);
                if (home is null || away is null)
                {
                    if (home is null) unmapped.Add(row.HomeRaw);
                    if (away is null) unmapped.Add(row.AwayRaw);
                    unmatched++;
                    continue;
                }

                if (OregonExclusion.IsExcluded("NFL", home, away))
                {
                    oregon++;
                    continue;
                }

                if (row.Date.Year is < MinSeason or > MaxSeason)
                    continue;
                inWindow++;

                var g = FindGame(byKey, byTeamsDate, row.Date, home, away);
                if (g is null)
                {
                    unmatched++;
                    continue;
                }

                if (haveReal.Contains(g.Id) || matchedIds.Contains(g.Id))
                {
                    skippedHave++;
                    continue;
                }

                // Append real-price O+C (over + under). Never delete assumed −110 rows.
                pending.Add(Snap(g.Id, "over", row.OpenTotal, AusNflXlsxParser.OverAmerican(row, PriceKind.Open),
                    PriceKind.Open, g.TipoffUtc.AddDays(-7)));
                pending.Add(Snap(g.Id, "under", row.OpenTotal, AusNflXlsxParser.UnderAmerican(row, PriceKind.Open),
                    PriceKind.Open, g.TipoffUtc.AddDays(-7)));
                pending.Add(Snap(g.Id, "over", row.CloseTotal, AusNflXlsxParser.OverAmerican(row, PriceKind.Close),
                    PriceKind.Close, g.TipoffUtc));
                pending.Add(Snap(g.Id, "under", row.CloseTotal, AusNflXlsxParser.UnderAmerican(row, PriceKind.Close),
                    PriceKind.Close, g.TipoffUtc));

                matchedIds.Add(g.Id);
                matched++;
            }
        }

        foreach (var chunk in pending.Chunk(2000))
        {
            db.OddsSnapshots.AddRange(chunk);
            await db.SaveChangesAsync(ct);
        }

        var seasonStats = await SeasonCoverageAsync(db, ct);

        Console.WriteLine(
            $"AUS NFL ({SourceBookLabel}): files={files.Length} parsed={parsed} window={inWindow} " +
            $"matched={matched} unmatched={unmatched} skipHaveReal={skippedHave} oregon={oregon} snaps={pending.Count}");
        if (unmapped.Count > 0)
            Console.WriteLine($"  unmapped teams: {string.Join(", ", unmapped.OrderBy(x => x).Take(20))}");
        Console.WriteLine("=== NFL seasons with AUS real-price O→C ===");
        foreach (var s in seasonStats)
            Console.WriteLine(
                $"  {s.Season}: O+C games={s.GamesWithOpenClose} real-price games={s.GamesWithRealPrice}");

        if (oregon > 0)
            throw new InvalidOperationException($"Oregon exclusion hit {oregon} AUS NFL row(s) — unexpected.");

        return new Result(files.Length, parsed, inWindow, matched, unmatched, skippedHave, oregon, pending.Count, seasonStats);
    }

    public static Game? FindGame(
        IReadOnlyDictionary<string, Game> byKey,
        IReadOnlyDictionary<string, Game> byTeamsDate,
        DateOnly date, string home, string away)
    {
        var key = SportTeams.GameKey("nfl", date, home, away);
        if (byKey.TryGetValue(key, out var g)) return g;
        // Late-window tipoffs can land on UTC date ±1 vs local kickoff date.
        foreach (var d in new[] { date, date.AddDays(-1), date.AddDays(1) })
        {
            if (byTeamsDate.TryGetValue(TeamDateKey(home, away, d), out g)) return g;
        }
        return null;
    }

    static string TeamDateKey(string home, string away, DateOnly date)
        => $"{home}|{away}|{date:yyyy-MM-dd}";

    static async Task<HashSet<int>> GamesWithRealAusAsync(AppDbContext db, CancellationToken ct)
    {
        var rows = await (
            from g in db.Games.AsNoTracking()
            where g.Sport == "NFL"
            join o in db.OddsSnapshots.AsNoTracking() on g.Id equals o.GameId
            where o.Market == "TOTAL" && o.Book == Book.Other && !o.AssumedJuice
            select new { g.Id, o.PriceKind }
        ).ToListAsync(ct);

        return rows.GroupBy(r => r.Id)
            .Where(g => g.Any(x => x.PriceKind == PriceKind.Open) && g.Any(x => x.PriceKind == PriceKind.Close))
            .Select(g => g.Key)
            .ToHashSet();
    }

    public static async Task<List<(string Season, int GamesWithOpenClose, int GamesWithRealPrice)>> SeasonCoverageAsync(
        AppDbContext db, CancellationToken ct = default)
    {
        var games = await db.Games.AsNoTracking()
            .Where(g => g.Sport == "NFL")
            .Select(g => new { g.Id, g.Season, Year = g.TipoffUtc.Year })
            .ToListAsync(ct);
        var snaps = await db.OddsSnapshots.AsNoTracking()
            .Where(o => o.Market == "TOTAL")
            .Select(o => new { o.GameId, o.PriceKind, o.AssumedJuice, o.Book })
            .ToListAsync(ct);
        var byGame = snaps.GroupBy(s => s.GameId).ToDictionary(g => g.Key, g => g.ToList());

        var seasons = new SortedDictionary<string, (int Oc, int Real)>(StringComparer.Ordinal);
        foreach (var g in games)
        {
            if (!byGame.TryGetValue(g.Id, out var list)) continue;
            var season = string.IsNullOrEmpty(g.Season)
                ? g.Year.ToString(CultureInfo.InvariantCulture)
                : g.Season!;
            seasons.TryGetValue(season, out var cur);
            var hasOpen = list.Any(x => x.PriceKind == PriceKind.Open);
            var hasClose = list.Any(x => x.PriceKind == PriceKind.Close);
            if (hasOpen && hasClose) cur.Oc++;
            if (list.Any(x => x.Book == Book.Other && !x.AssumedJuice && x.PriceKind == PriceKind.Open) &&
                list.Any(x => x.Book == Book.Other && !x.AssumedJuice && x.PriceKind == PriceKind.Close))
                cur.Real++;
            seasons[season] = cur;
        }

        return seasons
            .Where(kv => kv.Value.Real > 0 || (int.TryParse(kv.Key, out var y) && y is >= MinSeason and <= MaxSeason))
            .Select(kv => (kv.Key, kv.Value.Oc, kv.Value.Real))
            .ToList();
    }

    static OddsSnapshot Snap(int gameId, string side, double line, int american, PriceKind kind, DateTimeOffset at)
        => new()
        {
            GameId = gameId,
            Book = Book.Other,
            Market = "TOTAL",
            Side = side,
            Line = line,
            AmericanOdds = american,
            PriceKind = kind,
            ClvQuality = ClvQuality.OpenClose,
            AssumedJuice = false,
            CapturedAt = at,
        };
}
