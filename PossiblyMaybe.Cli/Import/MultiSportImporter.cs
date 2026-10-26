using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PossiblyMaybe.Data;
using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Cli.Import;

/// <summary>Append MLB / NFL games + totals into the warehouse (does not wipe NBA).</summary>
public static class MultiSportImporter
{
    public static async Task ImportMlbAsync(string rawRoot, string dbPath, CancellationToken ct = default)
    {
        var path = Path.Combine(rawRoot, "kaggle", "mlb_closing_odds_2025", "mlb_2025_closing_odds.csv");
        if (!File.Exists(path)) throw new FileNotFoundException(path);

        var cs = $"Data Source={dbPath}";
        await AppDbContext.EnsureCreatedAsync(cs, ct);
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(cs).Options;
        await using var db = new AppDbContext(options);
        await EnsurePitchingTableAsync(db, ct);

        var oldIds = await db.Games.Where(g => g.Sport == "MLB").Select(g => g.Id).ToListAsync(ct);
        if (oldIds.Count > 0)
        {
            await db.Database.ExecuteSqlRawAsync(
                $"DELETE FROM GamePitchings WHERE GameId IN ({string.Join(",", oldIds.DefaultIfEmpty(-1))})", ct);
            await db.OddsSnapshots.Where(o => oldIds.Contains(o.GameId)).ExecuteDeleteAsync(ct);
            await db.Games.Where(g => g.Sport == "MLB").ExecuteDeleteAsync(ct);
        }

        using var reader = new StreamReader(path);
        var header = await reader.ReadLineAsync(ct) ?? "";
        var cols = Split(header);
        int I(string n) => Array.FindIndex(cols, c => c.Equals(n, StringComparison.OrdinalIgnoreCase));

        var games = new List<Game>();
        var keyToGame = new Dictionary<string, Game>(StringComparer.Ordinal);
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            var c = Split(line);
            var home = SportTeams.TryMlb(c[I("home_team")]);
            var away = SportTeams.TryMlb(c[I("away_team")]);
            if (home is null || away is null) continue;
            var datePart = c[I("date_et")].Length >= 10 ? c[I("date_et")][..10] : c[I("date_et")];
            if (!DateOnly.TryParse(datePart, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                continue;
            if (!int.TryParse(c[I("home_score")], out var hs) || !int.TryParse(c[I("away_score")], out var as_))
                continue;
            if (!TryD(c[I("open_total")], out _) || !TryD(c[I("close_total")], out _))
                continue;

            var tip = DateTimeOffset.TryParse(c[I("start_time_utc")], CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var tipParsed)
                ? tipParsed
                : new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            var key = SportTeams.GameKey("mlb", date, home, away);
            if (keyToGame.ContainsKey(key)) continue;

            var g = new Game
            {
                Sport = "MLB",
                Season = "2025",
                SourceKey = key,
                TipoffUtc = tip,
                HomeTeam = home,
                AwayTeam = away,
                HomeScore = hs,
                AwayScore = as_,
            };
            keyToGame[key] = g;
            games.Add(g);
        }

        db.Games.AddRange(games);
        await db.SaveChangesAsync(ct);

        var snaps = new List<OddsSnapshot>();
        using var reader2 = new StreamReader(path);
        _ = await reader2.ReadLineAsync(ct);
        while (await reader2.ReadLineAsync(ct) is { } line)
        {
            var c = Split(line);
            var home = SportTeams.TryMlb(c[I("home_team")]);
            var away = SportTeams.TryMlb(c[I("away_team")]);
            if (home is null || away is null) continue;
            var datePart = c[I("date_et")].Length >= 10 ? c[I("date_et")][..10] : c[I("date_et")];
            if (!DateOnly.TryParse(datePart, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                continue;
            var key = SportTeams.GameKey("mlb", date, home, away);
            if (!keyToGame.TryGetValue(key, out var g)) continue;
            if (!TryD(c[I("open_total")], out var openT) || !TryD(c[I("close_total")], out var closeT))
                continue;
            snaps.Add(Total(g.Id, openT, PriceKind.Open, g.TipoffUtc.AddHours(-24)));
            snaps.Add(Total(g.Id, closeT, PriceKind.Close, g.TipoffUtc));
        }

        db.OddsSnapshots.AddRange(snaps);
        await db.SaveChangesAsync(ct);
        Console.WriteLine($"MLB import: games={games.Count} totalSnaps={snaps.Count}");

        await ImportMlbPitchersAsync(rawRoot, db, keyToGame, ct);
    }

    private static async Task ImportMlbPitchersAsync(
        string rawRoot, AppDbContext db, Dictionary<string, Game> keyToGame, CancellationToken ct)
    {
        var path = Path.Combine(rawRoot, "mlb", "games_sp_fip_2025.csv");
        if (!File.Exists(path))
        {
            Console.WriteLine("MLB pitchers: missing games_sp_fip_2025.csv (run scripts/fetch_mlb_sp.py)");
            return;
        }

        using var reader = new StreamReader(path);
        var header = await reader.ReadLineAsync(ct) ?? "";
        var cols = Split(header);
        int I(string n) => Array.FindIndex(cols, c => c.Equals(n, StringComparison.OrdinalIgnoreCase));
        await db.Database.ExecuteSqlRawAsync("DELETE FROM GamePitchings", ct);
        var rows = new Dictionary<int, GamePitching>();
        var lg = 4.0;
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            var c = Split(line);
            var home = SportTeams.TryMlb(c[I("home")]);
            var away = SportTeams.TryMlb(c[I("away")]);
            if (home is null || away is null) continue;
            if (!DateOnly.TryParse(c[I("date")], CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                continue;
            var key = SportTeams.GameKey("mlb", date, home, away);
            if (!keyToGame.TryGetValue(key, out var g)) continue;
            if (rows.ContainsKey(g.Id)) continue;
            double? hf = TryD(c[I("home_fip")], out var hfv) ? hfv : null;
            double? af = TryD(c[I("away_fip")], out var afv) ? afv : null;
            rows[g.Id] = new GamePitching
            {
                GameId = g.Id,
                HomeSp = c[I("home_sp")],
                AwaySp = c[I("away_sp")],
                HomeFipPrior = hf ?? lg,
                AwayFipPrior = af ?? lg,
            };
        }

        db.GamePitchings.AddRange(rows.Values);
        await db.SaveChangesAsync(ct);
        Console.WriteLine($"MLB pitchers: {rows.Count} (missing FIP → league {lg})");
    }

    public static async Task ImportMlsAsync(string rawRoot, string dbPath, CancellationToken ct = default)
    {
        var path = Path.Combine(rawRoot, "mls", "mls_totals_open_close.csv");
        if (!File.Exists(path))
            throw new FileNotFoundException($"Missing {path}. Run: python scripts/export_mls_totals.py");

        var cs = $"Data Source={dbPath}";
        await AppDbContext.EnsureCreatedAsync(cs, ct);
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(cs).Options;
        await using var db = new AppDbContext(options);
        await EnsureMlsXgTableAsync(db, ct);

        var oldIds = await db.Games.Where(g => g.Sport == "MLS").Select(g => g.Id).ToListAsync(ct);
        if (oldIds.Count > 0)
        {
            await db.Database.ExecuteSqlRawAsync(
                $"DELETE FROM MlsGameXgs WHERE GameId IN ({string.Join(",", oldIds.DefaultIfEmpty(-1))})", ct);
            await db.OddsSnapshots.Where(o => oldIds.Contains(o.GameId)).ExecuteDeleteAsync(ct);
            await db.Games.Where(g => g.Sport == "MLS").ExecuteDeleteAsync(ct);
        }

        using var reader = new StreamReader(path);
        var header = await reader.ReadLineAsync(ct) ?? "";
        var cols = Split(header);
        int I(string n) => Array.FindIndex(cols, c => c.Equals(n, StringComparison.OrdinalIgnoreCase));

        var games = new List<Game>();
        var keyToGame = new Dictionary<string, Game>(StringComparer.Ordinal);
        var pendingXg = new List<(string Key, double Home, double Away)>();
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            var c = Split(line);
            var home = SportTeams.TryMls(c[I("home_team")]);
            var away = SportTeams.TryMls(c[I("away_team")]);
            if (home is null || away is null) continue;
            if (!DateTimeOffset.TryParse(c[I("kickoff_utc")], CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var tip))
                continue;
            var date = DateOnly.FromDateTime(tip.UtcDateTime);
            if (!int.TryParse(c[I("home_score")], out var hs) || !int.TryParse(c[I("away_score")], out var as_))
                continue;
            if (!TryD(c[I("open_ou")], out _) || !TryD(c[I("close_ou")], out _))
                continue;
            var key = SportTeams.GameKey("mls", date, home, away);
            if (keyToGame.ContainsKey(key)) continue;

            var g = new Game
            {
                Sport = "MLS",
                Season = c[I("season")],
                SourceKey = key,
                TipoffUtc = tip,
                HomeTeam = home,
                AwayTeam = away,
                HomeScore = hs,
                AwayScore = as_,
            };
            keyToGame[key] = g;
            games.Add(g);
            if (TryD(Safe(c, I("home_xg")), out var hx) && TryD(Safe(c, I("away_xg")), out var ax))
                pendingXg.Add((key, hx, ax));
        }

        db.Games.AddRange(games);
        await db.SaveChangesAsync(ct);

        var snaps = new List<OddsSnapshot>();
        using var reader2 = new StreamReader(path);
        _ = await reader2.ReadLineAsync(ct);
        while (await reader2.ReadLineAsync(ct) is { } line)
        {
            var c = Split(line);
            var home = SportTeams.TryMls(c[I("home_team")]);
            var away = SportTeams.TryMls(c[I("away_team")]);
            if (home is null || away is null) continue;
            if (!DateTimeOffset.TryParse(c[I("kickoff_utc")], CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var tip))
                continue;
            var key = SportTeams.GameKey("mls", DateOnly.FromDateTime(tip.UtcDateTime), home, away);
            if (!keyToGame.TryGetValue(key, out var g)) continue;
            if (!TryD(c[I("open_ou")], out var openT) || !TryD(c[I("close_ou")], out var closeT))
                continue;
            snaps.Add(Total(g.Id, openT, PriceKind.Open, g.TipoffUtc.AddHours(-24)));
            snaps.Add(Total(g.Id, closeT, PriceKind.Close, g.TipoffUtc));
        }

        db.OddsSnapshots.AddRange(snaps);
        var xgRows = new List<MlsGameXg>();
        foreach (var (key, hx, ax) in pendingXg)
        {
            if (!keyToGame.TryGetValue(key, out var g)) continue;
            xgRows.Add(new MlsGameXg { GameId = g.Id, HomeXg = hx, AwayXg = ax });
        }
        db.MlsGameXgs.AddRange(xgRows);
        await db.SaveChangesAsync(ct);
        Console.WriteLine($"MLS import: games={games.Count} totalSnaps={snaps.Count} xg={xgRows.Count}");
    }

    public static async Task ImportNflAsync(string rawRoot, string dbPath, CancellationToken ct = default)
    {
        var openClose = Path.Combine(rawRoot, "kaggle", "nfl_odds_2026", "nfl_2026_odds.csv");
        var nflverse = Path.Combine(rawRoot, "nfl", "nflverse_games.csv");
        if (!File.Exists(nflverse))
            throw new FileNotFoundException(nflverse);

        var cs = $"Data Source={dbPath}";
        await AppDbContext.EnsureCreatedAsync(cs, ct);
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(cs).Options;
        await using var db = new AppDbContext(options);
        await EnsureNflContextTableAsync(db, ct);

        var oldIds = await db.Games.Where(g => g.Sport == "NFL").Select(g => g.Id).ToListAsync(ct);
        if (oldIds.Count > 0)
        {
            await db.Database.ExecuteSqlRawAsync(
                $"DELETE FROM NflGameContexts WHERE GameId IN ({string.Join(",", oldIds.DefaultIfEmpty(-1))})", ct);
            await db.OddsSnapshots.Where(o => oldIds.Contains(o.GameId)).ExecuteDeleteAsync(ct);
            await db.Games.Where(g => g.Sport == "NFL").ExecuteDeleteAsync(ct);
        }

        var keyToGame = new Dictionary<string, Game>(StringComparer.Ordinal);
        using var reader = new StreamReader(nflverse);
        var header = await reader.ReadLineAsync(ct) ?? "";
        var cols = Split(header);
        int I(string n) => Array.FindIndex(cols, c => c.Equals(n, StringComparison.OrdinalIgnoreCase));

        var contexts = new List<NflGameContext>();
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            var c = Split(line);
            if (c.Length <= Math.Max(I("total_line"), I("home_score"))) continue;
            var home = SportTeams.TryNfl(c[I("home_team")]);
            var away = SportTeams.TryNfl(c[I("away_team")]);
            if (home is null || away is null) continue;
            if (!DateOnly.TryParse(c[I("gameday")], CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                continue;
            if (!int.TryParse(c[I("home_score")], out var hs) || !int.TryParse(c[I("away_score")], out var as_))
                continue;
            if (!TryD(c[I("total_line")], out var closeT)) continue;
            var key = SportTeams.GameKey("nfl", date, home, away);
            if (keyToGame.ContainsKey(key)) continue;

            var g = new Game
            {
                Sport = "NFL",
                Season = c[I("season")],
                SourceKey = key,
                TipoffUtc = new DateTimeOffset(date.ToDateTime(new TimeOnly(17, 0)), TimeSpan.Zero),
                HomeTeam = home,
                AwayTeam = away,
                HomeScore = hs,
                AwayScore = as_,
            };
            keyToGame[key] = g;
            db.Games.Add(g);
        }

        await db.SaveChangesAsync(ct);

        var snaps = new List<OddsSnapshot>();
        using var reader2 = new StreamReader(nflverse);
        _ = await reader2.ReadLineAsync(ct);
        while (await reader2.ReadLineAsync(ct) is { } line)
        {
            var c = Split(line);
            var home = SportTeams.TryNfl(c[I("home_team")]);
            var away = SportTeams.TryNfl(c[I("away_team")]);
            if (home is null || away is null) continue;
            if (!DateOnly.TryParse(c[I("gameday")], CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                continue;
            var key = SportTeams.GameKey("nfl", date, home, away);
            if (!keyToGame.TryGetValue(key, out var g)) continue;
            if (!TryD(c[I("total_line")], out var closeT)) continue;

            snaps.Add(new OddsSnapshot
            {
                GameId = g.Id,
                Book = Book.Consensus,
                Market = "TOTAL",
                Side = "over",
                Line = closeT,
                AmericanOdds = -110,
                PriceKind = PriceKind.Close,
                ClvQuality = ClvQuality.CloseOnly,
                AssumedJuice = true,
                CapturedAt = g.TipoffUtc,
            });

            var roof = c[I("roof")];
            double? wind = TryD(Safe(c, I("wind")), out var wv) ? wv : null;
            double? temp = TryD(Safe(c, I("temp")), out var tv) ? tv : null;
            int.TryParse(Safe(c, I("home_rest")), out var hr);
            int.TryParse(Safe(c, I("away_rest")), out var ar);
            contexts.Add(new NflGameContext
            {
                GameId = g.Id,
                Roof = roof,
                WindMph = wind,
                TempF = temp,
                HomeRest = hr,
                AwayRest = ar,
            });
        }

        db.OddsSnapshots.AddRange(snaps);
        db.NflGameContexts.AddRange(contexts);
        await db.SaveChangesAsync(ct);
        Console.WriteLine($"NFL nflverse: games={keyToGame.Count} closeTotals={snaps.Count} contexts={contexts.Count}");

        if (!File.Exists(openClose)) return;

        using var reader3 = new StreamReader(openClose);
        var h3 = await reader3.ReadLineAsync(ct) ?? "";
        var cols3 = Split(h3);
        int J(string n) => Array.FindIndex(cols3, c => c.Equals(n, StringComparison.OrdinalIgnoreCase));
        var added = 0;
        while (await reader3.ReadLineAsync(ct) is { } line)
        {
            var c = Split(line);
            var home = SportTeams.TryNfl(c[J("home_team")]);
            var away = SportTeams.TryNfl(c[J("away_team")]);
            if (home is null || away is null) continue;
            var datePart = c[J("date_et")].Length >= 10 ? c[J("date_et")][..10] : c[J("date_et")];
            if (!DateOnly.TryParse(datePart, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                continue;
            if (!TryD(c[J("open_total")], out var openT) || !TryD(c[J("close_total")], out var closeT))
                continue;
            var key = SportTeams.GameKey("nfl", date, home, away);
            if (!keyToGame.TryGetValue(key, out var g))
            {
                int.TryParse(c[J("home_score")], out var hs);
                int.TryParse(c[J("away_score")], out var as_);
                g = new Game
                {
                    Sport = "NFL",
                    Season = c[J("season")],
                    SourceKey = key,
                    TipoffUtc = new DateTimeOffset(date.ToDateTime(new TimeOnly(17, 0)), TimeSpan.Zero),
                    HomeTeam = home,
                    AwayTeam = away,
                    HomeScore = hs,
                    AwayScore = as_,
                };
                db.Games.Add(g);
                await db.SaveChangesAsync(ct);
                keyToGame[key] = g;
            }

            await db.OddsSnapshots.Where(o => o.GameId == g.Id && o.Market == "TOTAL").ExecuteDeleteAsync(ct);
            db.OddsSnapshots.Add(Total(g.Id, openT, PriceKind.Open, g.TipoffUtc.AddDays(-7)));
            db.OddsSnapshots.Add(Total(g.Id, closeT, PriceKind.Close, g.TipoffUtc));
            added++;
        }

        await db.SaveChangesAsync(ct);
        Console.WriteLine($"NFL minmaxer open/close overlays: {added}");
    }

    private static async Task EnsurePitchingTableAsync(AppDbContext db, CancellationToken ct)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "GamePitchings" (
              "Id" INTEGER NOT NULL CONSTRAINT "PK_GamePitchings" PRIMARY KEY AUTOINCREMENT,
              "GameId" INTEGER NOT NULL,
              "HomeSp" TEXT NOT NULL,
              "AwaySp" TEXT NOT NULL,
              "HomeFipPrior" REAL NOT NULL,
              "AwayFipPrior" REAL NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_GamePitchings_GameId" ON "GamePitchings" ("GameId");
            """, ct);
    }

    private static async Task EnsureNflContextTableAsync(AppDbContext db, CancellationToken ct)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "NflGameContexts" (
              "Id" INTEGER NOT NULL CONSTRAINT "PK_NflGameContexts" PRIMARY KEY AUTOINCREMENT,
              "GameId" INTEGER NOT NULL,
              "Roof" TEXT NULL,
              "WindMph" REAL NULL,
              "TempF" REAL NULL,
              "HomeRest" INTEGER NOT NULL,
              "AwayRest" INTEGER NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_NflGameContexts_GameId" ON "NflGameContexts" ("GameId");
            """, ct);
    }

    private static async Task EnsureMlsXgTableAsync(AppDbContext db, CancellationToken ct)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "MlsGameXgs" (
              "Id" INTEGER NOT NULL CONSTRAINT "PK_MlsGameXgs" PRIMARY KEY AUTOINCREMENT,
              "GameId" INTEGER NOT NULL,
              "HomeXg" REAL NOT NULL,
              "AwayXg" REAL NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_MlsGameXgs_GameId" ON "MlsGameXgs" ("GameId");
            """, ct);
    }

    private static OddsSnapshot Total(int gameId, double line, PriceKind kind, DateTimeOffset at)
        => new()
        {
            GameId = gameId,
            Book = Book.Consensus,
            Market = "TOTAL",
            Side = "over",
            Line = line,
            AmericanOdds = -110,
            PriceKind = kind,
            ClvQuality = kind == PriceKind.Open ? ClvQuality.OpenClose : ClvQuality.OpenClose,
            AssumedJuice = true,
            CapturedAt = at,
        };

    private static string Safe(string[] c, int i) => i >= 0 && i < c.Length ? c[i] : "";

    private static string[] Split(string line)
    {
        var list = new List<string>();
        var cur = new System.Text.StringBuilder();
        var inQ = false;
        foreach (var ch in line)
        {
            if (ch == '"') { inQ = !inQ; continue; }
            if (ch == ',' && !inQ) { list.Add(cur.ToString()); cur.Clear(); continue; }
            cur.Append(ch);
        }
        list.Add(cur.ToString());
        return list.ToArray();
    }

    private static bool TryD(string s, out double v)
        => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v);
}
