using Microsoft.EntityFrameworkCore;
using PossiblyMaybe.Data;
using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Cli.Import;

/// <summary>
/// T2a: import SBRO NHL OpenOU/CloseOU totals (+ real over juice) into Games + OddsSnapshots.
/// Book=Consensus — O→C is present (ClvQuality=OpenClose) but is not same-book close for venue CLV.
/// Skips 2022-23 (mid-season only). <c>nhl_odds_2021.xlsx</c> is the 2020-21 short season.
/// </summary>
public static class NhlSbroImporter
{
    public sealed record SeasonCoverage(string Season, int Games, int OpenCloseGames, int RealJuiceSnaps);

    public static async Task<IReadOnlyList<SeasonCoverage>> ImportAsync(
        string dir, string dbPath, CancellationToken ct = default)
    {
        if (!Directory.Exists(dir))
            throw new DirectoryNotFoundException($"NHL SBRO folder missing: {dir}");

        var cs = $"Data Source={dbPath}";
        await AppDbContext.EnsureCreatedAsync(cs, ct);
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(cs).Options;
        await using var db = new AppDbContext(options);

        var files = Directory.GetFiles(dir, "*.xlsx")
            .Where(f => !Path.GetFileName(f).Contains("2022-23", StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

        var existing = await db.Games.Where(g => g.Sport == "NHL").ToListAsync(ct);
        var byKey = existing.ToDictionary(g => g.SourceKey, StringComparer.Ordinal);
        var openCloseHave = await GamesWithOpenCloseAsync(db, ct);

        var pendingGames = new List<Game>();
        var pendingSnaps = new List<(string Key, double Open, double Close, int OpenAm, int CloseAm, bool OpenAssumed, bool CloseAssumed, DateTimeOffset Tip)>();
        var replaceIds = new List<int>();
        var skippedHave = 0;
        var skippedMap = 0;
        var oregonHits = 0;
        var unmapped = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenNew = new HashSet<string>(StringComparer.Ordinal);

        foreach (var file in files)
        {
            var (y1, y2) = SbroXlsxParser.SeasonYearsFromPath(file);
            var seasonLabel = $"{y1}-{y2}";

            foreach (var row in SbroXlsxParser.ParseNhlFile(file))
            {
                var home = MapNhl(row.Home, unmapped);
                var away = MapNhl(row.Away, unmapped);
                if (home is null || away is null)
                {
                    skippedMap++;
                    continue;
                }

                if (OregonExclusion.IsExcluded("NHL", home, away))
                {
                    oregonHits++;
                    continue;
                }

                var key = SportTeams.GameKey("nhl", row.Date, home, away);
                if (openCloseHave.Contains(key) || seenNew.Contains(key))
                {
                    skippedHave++;
                    continue;
                }

                var tip = new DateTimeOffset(row.Date.ToDateTime(new TimeOnly(0, 0)), TimeSpan.Zero);
                if (byKey.TryGetValue(key, out var g))
                {
                    replaceIds.Add(g.Id);
                }
                else
                {
                    g = new Game
                    {
                        Sport = "NHL",
                        Season = seasonLabel,
                        SourceKey = key,
                        TipoffUtc = tip,
                        HomeTeam = home,
                        AwayTeam = away,
                        HomeScore = row.HomeScore,
                        AwayScore = row.AwayScore,
                    };
                    pendingGames.Add(g);
                    byKey[key] = g;
                }

                pendingSnaps.Add((key, row.OpenTotal, row.CloseTotal, row.OpenOverAmerican, row.CloseOverAmerican,
                    row.OpenJuiceAssumed, row.CloseJuiceAssumed, tip));
                seenNew.Add(key);
            }
        }

        if (oregonHits > 0)
            throw new InvalidOperationException(
                $"NHL SBRO import found {oregonHits} Oregon-excluded rows (expected 0).");

        if (replaceIds.Count > 0)
        {
            foreach (var chunk in replaceIds.Distinct().Chunk(500))
            {
                var ids = chunk.ToArray();
                await db.OddsSnapshots.Where(o => ids.Contains(o.GameId) && o.Market == "TOTAL")
                    .ExecuteDeleteAsync(ct);
            }
        }

        if (pendingGames.Count > 0)
        {
            db.Games.AddRange(pendingGames);
            await db.SaveChangesAsync(ct);
        }

        var snaps = new List<OddsSnapshot>(pendingSnaps.Count * 2);
        foreach (var (key, openT, closeT, openAm, closeAm, openAssumed, closeAssumed, tip) in pendingSnaps)
        {
            if (!byKey.TryGetValue(key, out var g) || g.Id == 0) continue;
            snaps.Add(TotalSnap(g.Id, openT, openAm, PriceKind.Open, tip.AddDays(-1), openAssumed));
            snaps.Add(TotalSnap(g.Id, closeT, closeAm, PriceKind.Close, tip, closeAssumed));
        }

        foreach (var chunk in snaps.Chunk(2000))
        {
            db.OddsSnapshots.AddRange(chunk);
            await db.SaveChangesAsync(ct);
        }

        Console.WriteLine(
            $"NHL SBRO: wrote {pendingSnaps.Count} O→C (skip have={skippedHave} map={skippedMap}; files={files.Length}; unmapped={unmapped.Count}; oregon=0)");
        if (unmapped.Count is > 0 and <= 40)
            Console.WriteLine($"  unmapped: {string.Join(", ", unmapped.OrderBy(x => x))}");
        else if (unmapped.Count > 40)
            Console.WriteLine($"  unmapped sample: {string.Join(", ", unmapped.OrderBy(x => x).Take(20))} …");

        var coverage = await CoverageAsync(db, ct);
        Console.WriteLine("=== NHL O→C by season ===");
        foreach (var row in coverage)
            Console.WriteLine(
                $"  {row.Season}: games={row.Games} O+C={row.OpenCloseGames} realJuiceSnaps={row.RealJuiceSnaps}");

        return coverage;
    }

    public static async Task<IReadOnlyList<SeasonCoverage>> CoverageAsync(
        AppDbContext db, CancellationToken ct = default)
    {
        var games = await db.Games.AsNoTracking()
            .Where(g => g.Sport == "NHL")
            .Select(g => new { g.Id, g.Season })
            .ToListAsync(ct);
        var snaps = await db.OddsSnapshots.AsNoTracking()
            .Where(o => o.Market == "TOTAL" && o.ClvQuality == ClvQuality.OpenClose)
            .Select(o => new { o.GameId, o.PriceKind, o.AssumedJuice })
            .ToListAsync(ct);

        var byGame = snaps.GroupBy(s => s.GameId).ToDictionary(
            g => g.Key,
            g => (
                HasOc: g.Any(x => x.PriceKind == PriceKind.Open) && g.Any(x => x.PriceKind == PriceKind.Close),
                Real: g.Count(x => !x.AssumedJuice)));

        return games
            .GroupBy(g => g.Season ?? "?")
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g =>
            {
                var ids = g.Select(x => x.Id).ToList();
                var oc = ids.Count(id => byGame.TryGetValue(id, out var t) && t.HasOc);
                var real = ids.Sum(id => byGame.TryGetValue(id, out var t) ? t.Real : 0);
                return new SeasonCoverage(g.Key, ids.Count, oc, real);
            })
            .ToList();
    }

    static string? MapNhl(string raw, HashSet<string> unmapped)
    {
        var c = SportTeams.TryNhl(raw);
        if (c is null)
        {
            // LosAngeles / NYIslanders / St.Louis
            var spaced = ExpandCamel(raw);
            c = SportTeams.TryNhl(spaced);
        }
        if (c is null) unmapped.Add(raw);
        return c;
    }

    static string ExpandCamel(string raw)
    {
        var sb = new System.Text.StringBuilder(raw.Length + 4);
        char prev = '\0';
        foreach (var ch in raw)
        {
            if (char.IsUpper(ch) && prev != '\0' && (char.IsLower(prev) || char.IsDigit(prev)))
                sb.Append(' ');
            sb.Append(ch);
            prev = ch;
        }
        return sb.ToString();
    }

    static async Task<HashSet<string>> GamesWithOpenCloseAsync(AppDbContext db, CancellationToken ct)
    {
        var rows = await (
            from g in db.Games.AsNoTracking()
            where g.Sport == "NHL"
            join o in db.OddsSnapshots.AsNoTracking() on g.Id equals o.GameId
            where o.Market == "TOTAL" && o.ClvQuality == ClvQuality.OpenClose
            select new { g.SourceKey, o.PriceKind }
        ).ToListAsync(ct);

        return rows.GroupBy(r => r.SourceKey)
            .Where(g => g.Any(x => x.PriceKind == PriceKind.Open) && g.Any(x => x.PriceKind == PriceKind.Close))
            .Select(g => g.Key)
            .ToHashSet(StringComparer.Ordinal);
    }

    static OddsSnapshot TotalSnap(
        int gameId, double line, int american, PriceKind kind, DateTimeOffset at, bool assumedJuice)
        => new()
        {
            GameId = gameId,
            Book = Book.Consensus, // honest: consensus O→C ≠ same-book venue close
            Market = "TOTAL",
            Side = "over",
            Line = line,
            AmericanOdds = american,
            PriceKind = kind,
            ClvQuality = ClvQuality.OpenClose,
            AssumedJuice = assumedJuice,
            CapturedAt = at,
        };
}
