using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PossiblyMaybe.Data;
using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Cli.Import;

/// <summary>
/// Append/overlay free O→C totals from data/raw/round2_odds (D1 hunt). Does not wipe NBA.
/// NFL: overlays OpenClose on existing nflverse games. MLB: appends 2011–2021 games + snaps.
/// </summary>
public static class Round2OddsImporter
{
    public static async Task ImportFlancastAsync(string rawRoot, string dbPath, CancellationToken ct = default)
    {
        var cs = $"Data Source={dbPath}";
        await AppDbContext.EnsureCreatedAsync(cs, ct);
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(cs).Options;
        await using var db = new AppDbContext(options);

        var nflPath = Path.Combine(rawRoot, "round2_odds", "github_sbr_scraper", "nfl_archive_10Y.json");
        var mlbPath = Path.Combine(rawRoot, "round2_odds", "github_sbr_scraper", "mlb_archive_10Y.json");
        if (File.Exists(nflPath))
            await OverlayNflFlancastAsync(db, nflPath, ct);
        else
            Console.WriteLine($"skip NFL flancast — missing {nflPath}");

        if (File.Exists(mlbPath))
            await AppendMlbFlancastAsync(db, mlbPath, ct);
        else
            Console.WriteLine($"skip MLB flancast — missing {mlbPath}");
    }

    static async Task OverlayNflFlancastAsync(AppDbContext db, string path, CancellationToken ct)
    {
        var games = await db.Games.Where(g => g.Sport == "NFL").ToListAsync(ct);
        var byKey = games.ToDictionary(g => g.SourceKey, StringComparer.Ordinal);
        var json = await File.ReadAllTextAsync(path, ct);
        using var doc = JsonDocument.Parse(json);
        var added = 0;
        var skipped = 0;
        foreach (var row in doc.RootElement.EnumerateArray())
        {
            if (!TryFlancastOu(row, out var date, out var openT, out var closeT, out var hs, out var as_))
            {
                skipped++;
                continue;
            }
            var home = SportTeams.TryNfl(JsonTeam(row, "home_team"));
            var away = SportTeams.TryNfl(JsonTeam(row, "away_team"));
            if (home is null || away is null)
            {
                skipped++;
                continue;
            }

            var key = SportTeams.GameKey("nfl", date, home, away);
            if (!byKey.TryGetValue(key, out var g))
            {
                var season = row.TryGetProperty("season", out var sEl) && sEl.TryGetInt32(out var sy)
                    ? sy.ToString()
                    : date.Year.ToString();
                g = new Game
                {
                    Sport = "NFL",
                    Season = season,
                    SourceKey = key,
                    TipoffUtc = new DateTimeOffset(date.ToDateTime(new TimeOnly(17, 0)), TimeSpan.Zero),
                    HomeTeam = home,
                    AwayTeam = away,
                    HomeScore = hs,
                    AwayScore = as_,
                };
                db.Games.Add(g);
                await db.SaveChangesAsync(ct);
                byKey[key] = g;
            }

            await db.OddsSnapshots.Where(o => o.GameId == g.Id && o.Market == "TOTAL").ExecuteDeleteAsync(ct);
            db.OddsSnapshots.Add(TotalSnap(g.Id, openT, PriceKind.Open, g.TipoffUtc.AddDays(-7)));
            db.OddsSnapshots.Add(TotalSnap(g.Id, closeT, PriceKind.Close, g.TipoffUtc));
            added++;
            if (added % 500 == 0)
                await db.SaveChangesAsync(ct);
        }

        await db.SaveChangesAsync(ct);
        Console.WriteLine($"NFL flancast O→C overlays: {added} (skipped {skipped})");
    }

    static async Task AppendMlbFlancastAsync(AppDbContext db, string path, CancellationToken ct)
    {
        var existing = await db.Games.Where(g => g.Sport == "MLB").Select(g => g.SourceKey).ToListAsync(ct);
        var have = existing.ToHashSet(StringComparer.Ordinal);
        var json = await File.ReadAllTextAsync(path, ct);
        using var doc = JsonDocument.Parse(json);

        var games = new List<Game>();
        var pendingSnaps = new List<(string Key, double Open, double Close)>();
        var skipped = 0;
        foreach (var row in doc.RootElement.EnumerateArray())
        {
            if (!TryFlancastOu(row, out var date, out var openT, out var closeT, out var hs, out var as_))
            {
                skipped++;
                continue;
            }
            var home = SportTeams.TryMlb(JsonTeam(row, "home_team"));
            var away = SportTeams.TryMlb(JsonTeam(row, "away_team"));
            if (home is null || away is null)
            {
                skipped++;
                continue;
            }

            var key = SportTeams.GameKey("mlb", date, home, away);
            if (have.Contains(key)) continue;

            var season = row.TryGetProperty("season", out var sEl) && sEl.TryGetInt32(out var sy)
                ? sy.ToString()
                : date.Year.ToString();
            var g = new Game
            {
                Sport = "MLB",
                Season = season,
                SourceKey = key,
                TipoffUtc = new DateTimeOffset(date.ToDateTime(new TimeOnly(23, 5)), TimeSpan.Zero),
                HomeTeam = home,
                AwayTeam = away,
                HomeScore = hs,
                AwayScore = as_,
            };
            games.Add(g);
            have.Add(key);
            pendingSnaps.Add((key, openT, closeT));
        }

        db.Games.AddRange(games);
        await db.SaveChangesAsync(ct);
        var byKey = games.ToDictionary(g => g.SourceKey, StringComparer.Ordinal);
        var snaps = new List<OddsSnapshot>();
        foreach (var (key, openT, closeT) in pendingSnaps)
        {
            if (!byKey.TryGetValue(key, out var g)) continue;
            snaps.Add(TotalSnap(g.Id, openT, PriceKind.Open, g.TipoffUtc.AddHours(-24)));
            snaps.Add(TotalSnap(g.Id, closeT, PriceKind.Close, g.TipoffUtc));
        }

        db.OddsSnapshots.AddRange(snaps);
        await db.SaveChangesAsync(ct);
        Console.WriteLine($"MLB flancast append: games={games.Count} snaps={snaps.Count} (skipped {skipped})");
    }

    static bool TryFlancastOu(
        JsonElement row, out DateOnly date, out double openT, out double closeT, out int? hs, out int? as_)
    {
        date = default;
        openT = closeT = 0;
        hs = as_ = null;
        if (!row.TryGetProperty("date", out var dEl)) return false;
        var dateNum = dEl.ValueKind == JsonValueKind.Number ? dEl.GetDouble() : 0;
        if (dateNum < 19000101) return false;
        var di = (int)dateNum;
        var y = di / 10000;
        var m = di / 100 % 100;
        var day = di % 100;
        try { date = new DateOnly(y, m, day); }
        catch { return false; }

        if (!TryNum(row, "open_over_under", out openT) || !TryNum(row, "close_over_under", out closeT))
            return false;
        // flancast sometimes stores 0 / missing as numeric zero — reject junk lines
        if (openT < 0.5 || closeT < 0.5) return false;
        if (row.TryGetProperty("home_final", out var hf)) hs = ParseScore(hf);
        if (row.TryGetProperty("away_final", out var af)) as_ = ParseScore(af);
        return true;
    }

    static string? JsonTeam(JsonElement row, string name)
    {
        if (!row.TryGetProperty(name, out var el)) return null;
        return el.ValueKind switch
        {
            JsonValueKind.String => el.GetString(),
            JsonValueKind.Number => el.GetRawText(),
            _ => el.ToString(),
        };
    }

    static bool TryNum(JsonElement row, string name, out double v)
    {
        v = 0;
        if (!row.TryGetProperty(name, out var el)) return false;
        return el.ValueKind switch
        {
            JsonValueKind.Number => el.TryGetDouble(out v),
            JsonValueKind.String => double.TryParse(el.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out v),
            _ => false,
        };
    }

    static int? ParseScore(JsonElement el)
        => el.ValueKind switch
        {
            JsonValueKind.Number when el.TryGetInt32(out var i) => i,
            JsonValueKind.String when int.TryParse(el.GetString(), out var i) => i,
            _ => null,
        };

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
