using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PossiblyMaybe.Data;
using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Cli.Import;

/// <summary>
/// Import on-disk O→C totals (W15): SBRO NFL/NBA/NCAAB/NCAAF + Arnav MLB.
/// Idempotent: skip a game when TOTAL Open+Close with ClvQuality=OpenClose already exist.
/// Oregon NCAA rows are imported for history completeness (exclusion stays at bet time).
/// </summary>
public static class DiskOddsImporter
{
    public static async Task ImportAsync(string rawRoot, string dbPath, CancellationToken ct = default)
    {
        var cs = $"Data Source={dbPath}";
        await AppDbContext.EnsureCreatedAsync(cs, ct);
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(cs).Options;
        await using var db = new AppDbContext(options);

        var before = await OpenCloseCoverageAsync(db, ct);
        PrintCoverage("BEFORE", before);

        await ImportSbroFolderAsync(db, Path.Combine(rawRoot, "round2_odds", "sbro_nfl"), "NFL", ct);
        await ImportSbroFolderAsync(db, Path.Combine(rawRoot, "round2_odds", "sbro_nba"), "NBA", ct);
        await ImportArnavMlbAsync(db, Path.Combine(rawRoot, "round2_odds", "github_mlb_arnav", "mlb_odds_dataset.json"), ct);
        await ImportSbroFolderAsync(db, Path.Combine(rawRoot, "round3_odds", "sbro_ncaab"), "NCAAB", ct);
        await ImportSbroFolderAsync(db, Path.Combine(rawRoot, "round3_odds", "sbro_ncaaf"), "NCAAF", ct);

        var after = await OpenCloseCoverageAsync(db, ct);
        PrintCoverage("AFTER", after);
        PrintDelta(before, after);
    }

    public static async Task<Dictionary<string, SortedSet<string>>> OpenCloseCoverageAsync(
        AppDbContext db, CancellationToken ct = default)
    {
        // Two-pass in memory — keeps SQLite/EF simple for coverage proof.
        var games = await db.Games.AsNoTracking()
            .Select(g => new { g.Id, g.Sport, g.Season, TipYear = g.TipoffUtc.Year })
            .ToListAsync(ct);
        var snaps = await db.OddsSnapshots.AsNoTracking()
            .Where(o => o.Market == "TOTAL" && o.ClvQuality == ClvQuality.OpenClose)
            .Select(o => new { o.GameId, o.PriceKind })
            .ToListAsync(ct);

        var byGame = snaps.GroupBy(s => s.GameId)
            .Where(g => g.Any(x => x.PriceKind == PriceKind.Open) && g.Any(x => x.PriceKind == PriceKind.Close))
            .Select(g => g.Key)
            .ToHashSet();

        var map = new Dictionary<string, SortedSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var g in games)
        {
            if (!byGame.Contains(g.Id)) continue;
            var season = string.IsNullOrEmpty(g.Season)
                ? g.TipYear.ToString(CultureInfo.InvariantCulture)
                : g.Season!;
            if (!map.TryGetValue(g.Sport, out var set))
                map[g.Sport] = set = new SortedSet<string>(StringComparer.Ordinal);
            set.Add(season);
        }
        return map;
    }

    static void PrintCoverage(string label, Dictionary<string, SortedSet<string>> cov)
    {
        Console.WriteLine($"=== O→C season coverage {label} ===");
        foreach (var sport in new[] { "NFL", "NBA", "MLB", "NCAAB", "NCAAF" })
        {
            if (!cov.TryGetValue(sport, out var seasons) || seasons.Count == 0)
            {
                Console.WriteLine($"  {sport}: (none)");
                continue;
            }
            Console.WriteLine($"  {sport}: {seasons.Count} seasons [{string.Join(", ", seasons)}]");
        }
    }

    static void PrintDelta(Dictionary<string, SortedSet<string>> before, Dictionary<string, SortedSet<string>> after)
    {
        Console.WriteLine("=== O→C seasons gained ===");
        foreach (var sport in new[] { "NFL", "NBA", "MLB", "NCAAB", "NCAAF" })
        {
            before.TryGetValue(sport, out var b);
            after.TryGetValue(sport, out var a);
            b ??= [];
            a ??= [];
            var gained = a.Except(b).ToList();
            Console.WriteLine($"  {sport}: {b.Count} → {a.Count}" +
                              (gained.Count > 0 ? $"  (+ {string.Join(", ", gained)})" : ""));
        }
    }

    static async Task ImportSbroFolderAsync(AppDbContext db, string dir, string sport, CancellationToken ct)
    {
        if (!Directory.Exists(dir))
        {
            Console.WriteLine($"skip {sport} SBRO — missing {dir}");
            return;
        }

        var files = Directory.GetFiles(dir, "*.xlsx").OrderBy(x => x, StringComparer.Ordinal).ToArray();
        var existing = await db.Games.Where(g => g.Sport == sport).ToListAsync(ct);
        var byKey = existing.ToDictionary(g => g.SourceKey, StringComparer.Ordinal);
        var openCloseHave = await GamesWithOpenCloseAsync(db, sport, ct);

        var pendingGames = new List<Game>();
        var pendingSnaps = new List<(string Key, double Open, double Close, DateTimeOffset Tip)>();
        var replaceIds = new List<int>();
        var skippedHave = 0;
        var skippedMap = 0;
        var unmapped = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenNew = new HashSet<string>(StringComparer.Ordinal);

        foreach (var file in files)
        {
            var (y1, _) = SbroXlsxParser.SeasonYearsFromPath(file);
            var seasonLabel = sport is "NCAAB" or "NCAAF"
                ? Path.GetFileNameWithoutExtension(file).Split('_').Last()
                : null; // NFL/NBA: prefer tipoff year when creating

            foreach (var row in SbroXlsxParser.ParseFile(file))
            {
                var home = MapTeam(sport, row.Home, unmapped);
                var away = MapTeam(sport, row.Away, unmapped);
                if (home is null || away is null)
                {
                    skippedMap++;
                    continue;
                }

                var key = SportTeams.GameKey(sport.ToLowerInvariant(), row.Date, home, away);
                if (openCloseHave.Contains(key) || seenNew.Contains(key))
                {
                    skippedHave++;
                    continue;
                }

                var tip = new DateTimeOffset(row.Date.ToDateTime(new TimeOnly(17, 0)), TimeSpan.Zero);
                if (byKey.TryGetValue(key, out var g))
                {
                    replaceIds.Add(g.Id);
                }
                else
                {
                    g = new Game
                    {
                        Sport = sport,
                        Season = seasonLabel ?? row.Date.Year.ToString(CultureInfo.InvariantCulture),
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

                pendingSnaps.Add((key, row.OpenTotal, row.CloseTotal, tip));
                seenNew.Add(key);
            }
        }

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
        foreach (var (key, openT, closeT, tip) in pendingSnaps)
        {
            if (!byKey.TryGetValue(key, out var g) || g.Id == 0) continue;
            snaps.Add(TotalSnap(g.Id, openT, PriceKind.Open, tip.AddDays(-7)));
            snaps.Add(TotalSnap(g.Id, closeT, PriceKind.Close, tip));
        }

        foreach (var chunk in snaps.Chunk(2000))
        {
            db.OddsSnapshots.AddRange(chunk);
            await db.SaveChangesAsync(ct);
        }

        Console.WriteLine(
            $"{sport} SBRO: wrote {pendingSnaps.Count} O→C (skip have={skippedHave} map={skippedMap}; files={files.Length}; unmappedNames={unmapped.Count})");
        if (unmapped.Count is > 0 and <= 40)
            Console.WriteLine($"  unmapped sample: {string.Join(", ", unmapped.OrderBy(x => x).Take(40))}");
        else if (unmapped.Count > 40)
            Console.WriteLine($"  unmapped sample: {string.Join(", ", unmapped.OrderBy(x => x).Take(20))} …");
    }

    static string? MapTeam(string sport, string raw, HashSet<string> unmapped)
    {
        switch (sport)
        {
            case "NFL":
            {
                var c = SportTeams.TryNfl(raw) ?? SportTeams.TryNfl(ExpandCamel(raw));
                if (c is null) unmapped.Add(raw);
                return c;
            }
            case "NBA":
            {
                var c = TeamCodes.TryNormalize(raw) ?? TeamCodes.TryNormalize(ExpandCamel(raw));
                if (c is null) unmapped.Add(raw);
                return c;
            }
            case "NCAAB":
            case "NCAAF":
                return SportTeams.NcaaCode(raw);
            default:
                unmapped.Add(raw);
                return null;
        }
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

    static async Task<HashSet<string>> GamesWithOpenCloseAsync(AppDbContext db, string sport, CancellationToken ct)
    {
        var rows = await (
            from g in db.Games.AsNoTracking()
            where g.Sport == sport
            join o in db.OddsSnapshots.AsNoTracking() on g.Id equals o.GameId
            where o.Market == "TOTAL" && o.ClvQuality == ClvQuality.OpenClose
            select new { g.SourceKey, o.PriceKind }
        ).ToListAsync(ct);

        return rows.GroupBy(r => r.SourceKey)
            .Where(g => g.Any(x => x.PriceKind == PriceKind.Open) && g.Any(x => x.PriceKind == PriceKind.Close))
            .Select(g => g.Key)
            .ToHashSet(StringComparer.Ordinal);
    }

    static async Task ImportArnavMlbAsync(AppDbContext db, string path, CancellationToken ct)
    {
        if (!File.Exists(path))
        {
            Console.WriteLine($"skip MLB Arnav — missing {path}");
            return;
        }

        var existing = await db.Games.Where(g => g.Sport == "MLB").ToListAsync(ct);
        var byKey = existing.ToDictionary(g => g.SourceKey, StringComparer.Ordinal);
        var openCloseHave = await GamesWithOpenCloseAsync(db, "MLB", ct);

        await using var fs = File.OpenRead(path);
        using var doc = await JsonDocument.ParseAsync(fs, cancellationToken: ct);

        var pendingGames = new List<Game>();
        var pendingSnaps = new List<(string Key, double Open, double Close, DateTimeOffset Tip)>();
        var replaceIds = new List<int>();
        var seenNew = new HashSet<string>(StringComparer.Ordinal);
        var skipped = 0;

        foreach (var day in doc.RootElement.EnumerateObject())
        {
            if (!DateOnly.TryParse(day.Name, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                continue;
            foreach (var row in day.Value.EnumerateArray())
            {
                if (!row.TryGetProperty("gameView", out var gv)) { skipped++; continue; }
                var homeRaw = TeamFromView(gv, "homeTeam");
                var awayRaw = TeamFromView(gv, "awayTeam");
                var home = SportTeams.TryMlb(homeRaw) ?? SportTeams.TryMlb(ShortFromView(gv, "homeTeam"));
                var away = SportTeams.TryMlb(awayRaw) ?? SportTeams.TryMlb(ShortFromView(gv, "awayTeam"));
                if (home is null || away is null) { skipped++; continue; }
                if (!TryArnavTotals(row, out var openT, out var closeT)) { skipped++; continue; }

                var key = SportTeams.GameKey("mlb", date, home, away);
                if (openCloseHave.Contains(key) || seenNew.Contains(key)) { skipped++; continue; }

                var tip = TipFromView(gv, date);
                if (byKey.TryGetValue(key, out var g))
                {
                    replaceIds.Add(g.Id);
                }
                else
                {
                    g = new Game
                    {
                        Sport = "MLB",
                        Season = date.Year.ToString(CultureInfo.InvariantCulture),
                        SourceKey = key,
                        TipoffUtc = tip,
                        HomeTeam = home,
                        AwayTeam = away,
                        HomeScore = ScoreFromView(gv, "homeTeamScore"),
                        AwayScore = ScoreFromView(gv, "awayTeamScore"),
                    };
                    pendingGames.Add(g);
                    byKey[key] = g;
                }

                pendingSnaps.Add((key, openT, closeT, tip));
                seenNew.Add(key);
            }
        }

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
        foreach (var (key, openT, closeT, tip) in pendingSnaps)
        {
            if (!byKey.TryGetValue(key, out var g) || g.Id == 0) continue;
            snaps.Add(TotalSnap(g.Id, openT, PriceKind.Open, tip.AddHours(-24)));
            snaps.Add(TotalSnap(g.Id, closeT, PriceKind.Close, tip));
        }

        foreach (var chunk in snaps.Chunk(2000))
        {
            db.OddsSnapshots.AddRange(chunk);
            await db.SaveChangesAsync(ct);
        }

        Console.WriteLine($"MLB Arnav: wrote {pendingSnaps.Count} O→C (skipped {skipped})");
    }

    static string? TeamFromView(JsonElement gv, string prop)
    {
        if (!gv.TryGetProperty(prop, out var t)) return null;
        if (t.TryGetProperty("fullName", out var fn)) return fn.GetString();
        if (t.TryGetProperty("nickname", out var nick)) return nick.GetString();
        return t.TryGetProperty("name", out var n) ? n.GetString() : null;
    }

    static string? ShortFromView(JsonElement gv, string prop)
        => gv.TryGetProperty(prop, out var t) && t.TryGetProperty("shortName", out var s) ? s.GetString() : null;

    static int? ScoreFromView(JsonElement gv, string prop)
        => gv.TryGetProperty(prop, out var el) && el.TryGetInt32(out var n) ? n : null;

    static DateTimeOffset TipFromView(JsonElement gv, DateOnly date)
    {
        if (gv.TryGetProperty("startDate", out var sd) &&
            DateTimeOffset.TryParse(sd.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var tip))
            return tip.ToUniversalTime();
        return new DateTimeOffset(date.ToDateTime(new TimeOnly(23, 5)), TimeSpan.Zero);
    }

    static bool TryArnavTotals(JsonElement row, out double openT, out double closeT)
    {
        openT = closeT = 0;
        if (!row.TryGetProperty("odds", out var odds) || !odds.TryGetProperty("totals", out var totals))
            return false;
        var opens = new List<double>();
        var closes = new List<double>();
        foreach (var book in totals.EnumerateArray())
        {
            if (book.TryGetProperty("openingLine", out var ol) && ol.TryGetProperty("total", out var ot) &&
                ot.TryGetDouble(out var ov) && ov >= 0.5)
                opens.Add(ov);
            if (book.TryGetProperty("currentLine", out var cl) && cl.TryGetProperty("total", out var ct) &&
                ct.TryGetDouble(out var cv) && cv >= 0.5)
                closes.Add(cv);
        }
        if (opens.Count == 0 || closes.Count == 0) return false;
        opens.Sort();
        closes.Sort();
        openT = opens[opens.Count / 2];
        closeT = closes[closes.Count / 2];
        return true;
    }

    static OddsSnapshot TotalSnap(int gameId, double line, PriceKind kind, DateTimeOffset at)
        => new()
        {
            GameId = gameId,
            Book = Book.Consensus,
            Market = "TOTAL",
            Side = "over",
            Line = line,
            AmericanOdds = -110,
            PriceKind = kind,
            ClvQuality = ClvQuality.OpenClose,
            AssumedJuice = true,
            CapturedAt = at,
        };
}
