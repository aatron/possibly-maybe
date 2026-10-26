using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PossiblyMaybe.Data;
using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Cli.Import;

public sealed class Gate0ImportReport
{
    public int Games { get; set; }
    public int Odds { get; set; }
    public int OddsOpenClose { get; set; }
    public int OddsCloseOnly { get; set; }
    public int OddsAssumedJuice { get; set; }
    public int Refs { get; set; }
    public int DonaghyFlagged { get; set; }
    public int Holdout2024Games { get; set; }
    public int Holdout2024WithMlClose { get; set; }
    public int Holdout2024WithRefs { get; set; }
    public int EspnIdsAttached { get; set; }
}

public static class Gate0Importer
{
    public static async Task<Gate0ImportReport> RunAsync(string rawRoot, string dbPath, CancellationToken ct = default)
    {
        var aCsv = Path.Combine(rawRoot, "kaggle", "A_nba-betting-data", "nba_2008-2026.csv");
        var bSqlite = Path.Combine(rawRoot, "kaggle", "B_basketball-betting-dataset", "basketball-final.sqlite");
        var cCsv = Path.Combine(rawRoot, "kaggle", "C_mgm-grand-nba-betting-data", "all_odds.csv");
        var officialsDir = Path.Combine(rawRoot, "espn_nba_officials");
        var schedulesDir = Path.Combine(rawRoot, "espn_nba_schedules");

        foreach (var p in new[] { aCsv, bSqlite, cCsv })
        {
            if (!File.Exists(p))
                throw new FileNotFoundException($"Missing raw file: {p}");
        }

        if (File.Exists(dbPath))
            File.Delete(dbPath);

        var cs = $"Data Source={dbPath}";
        await AppDbContext.EnsureCreatedAsync(cs, ct);
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(cs).Options;
        await using var db = new AppDbContext(options);

        var gamesByKey = new Dictionary<string, Game>(StringComparer.Ordinal);
        await ImportGamesFromAAsync(db, aCsv, gamesByKey, ct);
        AttachEspnIds(schedulesDir, gamesByKey);
        await ImportOddsFromBAsync(db, bSqlite, gamesByKey, ct);
        await ImportOddsFromAAsync(db, aCsv, gamesByKey, ct);
        await ImportOddsFromCAsync(db, cCsv, gamesByKey, ct);
        await ImportOddsFromEAsync(db, Path.Combine(rawRoot, "kaggle", "E_nba-closing-lines-ats-2024-25", "nba_2024_25_closing_lines_ats.csv"), gamesByKey, ct);
        await ImportOddsFromEspnOpenCloseAsync(
            db,
            Path.Combine(rawRoot, "espn_ml_open_close", "espn_ml_open_close.csv"),
            gamesByKey,
            ct);
        await ImportSpreadsFromBAsync(db, bSqlite, gamesByKey, ct);
        await ImportSpreadsFromEspnAsync(
            db,
            Path.Combine(rawRoot, "espn_ml_open_close", "espn_spread_open_close.csv"),
            gamesByKey,
            ct);
        await ImportTotalsFromBAsync(db, bSqlite, gamesByKey, ct);
        await ImportTotalsFromEAsync(
            db,
            Path.Combine(rawRoot, "kaggle", "E_nba-closing-lines-ats-2024-25", "nba_2024_25_closing_lines_ats.csv"),
            gamesByKey,
            ct);
        // D first (holdouts); B fills earlier seasons + Donaghy
        await ImportRefsFromDAsync(db, officialsDir, gamesByKey, ct);
        await ImportRefsFromBAsync(db, bSqlite, gamesByKey, ct);

        await db.SaveChangesAsync(ct);

        var report = await BuildReportAsync(db, ct);
        return report;
    }

    private static async Task ImportGamesFromAAsync(
        AppDbContext db, string path, Dictionary<string, Game> gamesByKey, CancellationToken ct)
    {
        using var reader = new StreamReader(path);
        var header = await reader.ReadLineAsync(ct) ?? throw new InvalidDataException("A CSV empty");
        var cols = SplitCsv(header);
        var iSeason = Idx(cols, "season");
        var iDate = Idx(cols, "date");
        var iAway = Idx(cols, "away");
        var iHome = Idx(cols, "home");
        var iSa = Idx(cols, "score_away");
        var iSh = Idx(cols, "score_home");

        var batch = new List<Game>(2048);
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            var c = SplitCsv(line);
            if (c.Length <= iSh) continue;
            var home = TeamCodes.TryNormalize(c[iHome]);
            var away = TeamCodes.TryNormalize(c[iAway]);
            if (home is null || away is null) continue;
            if (!DateOnly.TryParse(c[iDate], CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                continue;

            var key = TeamCodes.GameKey(date, home, away);
            if (gamesByKey.ContainsKey(key)) continue;

            var tip = new DateTimeOffset(date.ToDateTime(new TimeOnly(0, 0), DateTimeKind.Utc));
            var g = new Game
            {
                Sport = "NBA",
                SourceKey = key,
                Season = c[iSeason],
                TipoffUtc = tip,
                HomeTeam = home,
                AwayTeam = away,
                HomeScore = ParseInt(c[iSh]),
                AwayScore = ParseInt(c[iSa]),
            };
            gamesByKey[key] = g;
            batch.Add(g);
        }

        db.Games.AddRange(batch);
        await db.SaveChangesAsync(ct);
    }

    private static void AttachEspnIds(string schedulesDir, Dictionary<string, Game> gamesByKey)
    {
        // Clean single-line map (preferred when present) — schedule CSVs have multiline quoted fields.
        var mapPath = Path.Combine(Directory.GetParent(schedulesDir)!.FullName, "espn_nba_officials", "espn_game_map.csv");
        if (File.Exists(mapPath))
        {
            foreach (var c in ReadCsvRecords(mapPath).Skip(1))
            {
                if (c.Length < 4) continue;
                var home = TeamCodes.TryNormalize(c[3]);
                var away = TeamCodes.TryNormalize(c[2]);
                if (home is null || away is null) continue;
                if (!DateOnly.TryParse(c[1].Length >= 10 ? c[1][..10] : c[1],
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                    continue;
                var key = TeamCodes.GameKey(date, home, away);
                if (gamesByKey.TryGetValue(key, out var g) && string.IsNullOrEmpty(g.EspnGameId))
                    g.EspnGameId = c[0];
            }
        }

        if (!Directory.Exists(schedulesDir)) return;
        foreach (var file in Directory.EnumerateFiles(schedulesDir, "nba_schedule_*.csv"))
        {
            using var enumRows = ReadCsvRecords(file).GetEnumerator();
            if (!enumRows.MoveNext()) continue;
            var cols = enumRows.Current;
            var iGameId = Idx(cols, "game_id");
            var iDate = Idx(cols, "game_date");
            var iHome = Idx(cols, "home_abbreviation");
            var iAway = Idx(cols, "away_abbreviation");
            while (enumRows.MoveNext())
            {
                var c = enumRows.Current;
                if (c.Length <= Math.Max(Math.Max(iGameId, iDate), Math.Max(iHome, iAway))) continue;
                var home = TeamCodes.TryNormalize(c[iHome]);
                var away = TeamCodes.TryNormalize(c[iAway]);
                if (home is null || away is null) continue;
                if (!DateOnly.TryParse(c[iDate].Length >= 10 ? c[iDate][..10] : c[iDate],
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                    continue;
                var key = TeamCodes.GameKey(date, home, away);
                if (gamesByKey.TryGetValue(key, out var g) && string.IsNullOrEmpty(g.EspnGameId))
                    g.EspnGameId = c[iGameId];
            }
        }
    }

    private static async Task ImportOddsFromBAsync(
        AppDbContext db, string sqlitePath, Dictionary<string, Game> gamesByKey, CancellationToken ct)
    {
        await using var conn = new SqliteConnection($"Data Source={sqlitePath}");
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT Date, HomeTeam, AwayTeam, HomeML, AwayML
            FROM BettingOdds_History
            """;
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var snaps = new List<OddsSnapshot>(4096);
        while (await r.ReadAsync(ct))
        {
            var dateStr = r.GetString(0);
            if (!TryParseDateOnly(dateStr, out var date))
                continue;
            var home = TeamCodes.TryNormalize(r.GetString(1));
            var away = TeamCodes.TryNormalize(r.GetString(2));
            if (home is null || away is null) continue;
            if (!gamesByKey.TryGetValue(TeamCodes.GameKey(date, home, away), out var g)) continue;

            var homeMl = ParseAmerican(r.IsDBNull(3) ? null : r.GetString(3));
            var awayMl = ParseAmerican(r.IsDBNull(4) ? null : r.GetString(4));
            if (homeMl is null || awayMl is null) continue;

            var captured = g.TipoffUtc;
            // B has no separate open ML column — store close only (honest ClvQuality).
            snaps.Add(Ml(g.Id, Book.Consensus, "home", homeMl.Value, PriceKind.Close, ClvQuality.CloseOnly, captured));
            snaps.Add(Ml(g.Id, Book.Consensus, "away", awayMl.Value, PriceKind.Close, ClvQuality.CloseOnly, captured));
        }

        db.OddsSnapshots.AddRange(snaps);
        await db.SaveChangesAsync(ct);
    }

    private static async Task ImportOddsFromAAsync(
        AppDbContext db, string path, Dictionary<string, Game> gamesByKey, CancellationToken ct)
    {
        // Only add close ML when game has no ML close yet.
        var haveClose = (await db.OddsSnapshots
                .Where(o => o.Market == "ML" && o.PriceKind == PriceKind.Close)
                .Select(o => o.GameId)
                .Distinct()
                .ToListAsync(ct))
            .ToHashSet();

        using var reader = new StreamReader(path);
        _ = await reader.ReadLineAsync(ct);
        var snaps = new List<OddsSnapshot>();
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            var c = SplitCsv(line);
            if (c.Length < 23) continue;
            var home = TeamCodes.TryNormalize(c[5]);
            var away = TeamCodes.TryNormalize(c[4]);
            if (home is null || away is null) continue;
            if (!DateOnly.TryParse(c[1], CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                continue;
            if (!gamesByKey.TryGetValue(TeamCodes.GameKey(date, home, away), out var g)) continue;
            if (haveClose.Contains(g.Id)) continue;

            var awayMl = ParseAmerican(c[21]);
            var homeMl = ParseAmerican(c[22]);
            if (homeMl is null || awayMl is null) continue;

            snaps.Add(Ml(g.Id, Book.Other, "home", homeMl.Value, PriceKind.Close, ClvQuality.CloseOnly, g.TipoffUtc));
            snaps.Add(Ml(g.Id, Book.Other, "away", awayMl.Value, PriceKind.Close, ClvQuality.CloseOnly, g.TipoffUtc));
            haveClose.Add(g.Id);
        }

        db.OddsSnapshots.AddRange(snaps);
        await db.SaveChangesAsync(ct);
    }

    private static async Task ImportOddsFromCAsync(
        AppDbContext db, string path, Dictionary<string, Game> gamesByKey, CancellationToken ct)
    {
        var haveClose = (await db.OddsSnapshots
                .Where(o => o.Market == "ML" && o.PriceKind == PriceKind.Close)
                .Select(o => o.GameId)
                .Distinct()
                .ToListAsync(ct))
            .ToHashSet();

        using var reader = new StreamReader(path);
        var header = await reader.ReadLineAsync(ct);
        if (header is null) return;
        var cols = SplitCsv(header);
        // leading empty column name in C
        var iDate = Idx(cols, "game_date");
        var iAway = Idx(cols, "away_team");
        var iHome = Idx(cols, "home_team");
        var iHomeMl = Idx(cols, "money_home_odds");
        var iAwayMl = Idx(cols, "money_away_odds");

        var snaps = new List<OddsSnapshot>();
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            var c = SplitCsv(line);
            if (c.Length <= iAwayMl) continue;
            var home = TeamCodes.TryNormalize(c[iHome]);
            var away = TeamCodes.TryNormalize(c[iAway]);
            if (home is null || away is null) continue;
            var dateTok = c[iDate];
            var datePart = dateTok.Length >= 10 ? dateTok[..10] : dateTok;
            if (!DateOnly.TryParse(datePart, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                continue;
            if (!gamesByKey.TryGetValue(TeamCodes.GameKey(date, home, away), out var g)) continue;
            if (haveClose.Contains(g.Id)) continue;

            var homeMl = ParseAmerican(c[iHomeMl]);
            var awayMl = ParseAmerican(c[iAwayMl]);
            if (homeMl is null || awayMl is null) continue;

            snaps.Add(Ml(g.Id, Book.Mgm, "home", homeMl.Value, PriceKind.Close, ClvQuality.CloseOnly, g.TipoffUtc));
            snaps.Add(Ml(g.Id, Book.Mgm, "away", awayMl.Value, PriceKind.Close, ClvQuality.CloseOnly, g.TipoffUtc));
            haveClose.Add(g.Id);
        }

        db.OddsSnapshots.AddRange(snaps);
        await db.SaveChangesAsync(ct);
    }

    private static async Task ImportOddsFromEAsync(
        AppDbContext db, string path, Dictionary<string, Game> gamesByKey, CancellationToken ct)
    {
        if (!File.Exists(path)) return;
        using var reader = new StreamReader(path);
        var header = await reader.ReadLineAsync(ct);
        if (header is null) return;
        var cols = SplitCsv(header);
        var iDate = Idx(cols, "date_et");
        var iHome = Idx(cols, "home_team");
        var iAway = Idx(cols, "away_team");
        var iOpen = Idx(cols, "open_home_win_prob");
        var iClose = Idx(cols, "close_home_win_prob");

        var snaps = new List<OddsSnapshot>();
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            var c = SplitCsv(line);
            if (c.Length <= iClose) continue;
            var home = TeamCodes.TryNormalize(c[iHome]);
            var away = TeamCodes.TryNormalize(c[iAway]);
            if (home is null || away is null) continue;
            var datePart = c[iDate].Length >= 10 ? c[iDate][..10] : c[iDate];
            if (!DateOnly.TryParse(datePart, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                continue;
            if (!gamesByKey.TryGetValue(TeamCodes.GameKey(date, home, away), out var g)) continue;
            if (!double.TryParse(c[iOpen], NumberStyles.Float, CultureInfo.InvariantCulture, out var openP)) continue;
            if (!double.TryParse(c[iClose], NumberStyles.Float, CultureInfo.InvariantCulture, out var closeP)) continue;
            openP = Math.Clamp(openP, 0.01, 0.99);
            closeP = Math.Clamp(closeP, 0.01, 0.99);
            var openHome = ProbToAmerican(openP);
            var openAway = ProbToAmerican(1 - openP);
            var closeHome = ProbToAmerican(closeP);
            var closeAway = ProbToAmerican(1 - closeP);
            snaps.Add(Ml(g.Id, Book.Consensus, "home", openHome, PriceKind.Open, ClvQuality.OpenClose, g.TipoffUtc.AddHours(-24)));
            snaps.Add(Ml(g.Id, Book.Consensus, "away", openAway, PriceKind.Open, ClvQuality.OpenClose, g.TipoffUtc.AddHours(-24)));
            snaps.Add(Ml(g.Id, Book.Consensus, "home", closeHome, PriceKind.Close, ClvQuality.OpenClose, g.TipoffUtc));
            snaps.Add(Ml(g.Id, Book.Consensus, "away", closeAway, PriceKind.Close, ClvQuality.OpenClose, g.TipoffUtc));
        }

        db.OddsSnapshots.AddRange(snaps);
        await db.SaveChangesAsync(ct);
    }

    private static int ProbToAmerican(double p)
    {
        var dec = 1.0 / p;
        return OddsMath.DecimalToAmerican(dec);
    }

    /// <summary>ESPN core API open/close ML (seasons ~2024+ retain open); primary holdout CLV source.</summary>
    private static async Task ImportOddsFromEspnOpenCloseAsync(
        AppDbContext db, string path, Dictionary<string, Game> gamesByKey, CancellationToken ct)
    {
        if (!File.Exists(path)) return;
        using var reader = new StreamReader(path);
        var header = await reader.ReadLineAsync(ct);
        if (header is null) return;
        var cols = SplitCsv(header);
        var iDate = Idx(cols, "date");
        var iHome = Idx(cols, "home");
        var iAway = Idx(cols, "away");
        var iProv = Idx(cols, "provider");
        var iHo = Idx(cols, "home_ml_open");
        var iHc = Idx(cols, "home_ml_close");
        var iAo = Idx(cols, "away_ml_open");
        var iAc = Idx(cols, "away_ml_close");

        var snaps = new List<OddsSnapshot>();
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            var c = SplitCsv(line);
            if (c.Length <= iAc) continue;
            var home = TeamCodes.TryNormalize(c[iHome]);
            var away = TeamCodes.TryNormalize(c[iAway]);
            if (home is null || away is null) continue;
            if (!DateOnly.TryParse(c[iDate], CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                continue;
            if (!gamesByKey.TryGetValue(TeamCodes.GameKey(date, home, away), out var g)) continue;

            var ho = ParseAmerican(c[iHo]);
            var hc = ParseAmerican(c[iHc]);
            var ao = ParseAmerican(c[iAo]);
            var ac = ParseAmerican(c[iAc]);
            if (ho is null || hc is null || ao is null || ac is null) continue;

            var book = MapEspnBook(c[iProv]);
            snaps.Add(Ml(g.Id, book, "home", ho.Value, PriceKind.Open, ClvQuality.OpenClose, g.TipoffUtc.AddHours(-24)));
            snaps.Add(Ml(g.Id, book, "away", ao.Value, PriceKind.Open, ClvQuality.OpenClose, g.TipoffUtc.AddHours(-24)));
            snaps.Add(Ml(g.Id, book, "home", hc.Value, PriceKind.Close, ClvQuality.OpenClose, g.TipoffUtc));
            snaps.Add(Ml(g.Id, book, "away", ac.Value, PriceKind.Close, ClvQuality.OpenClose, g.TipoffUtc));
        }

        db.OddsSnapshots.AddRange(snaps);
        await db.SaveChangesAsync(ct);
    }

    private static Book MapEspnBook(string provider)
    {
        var p = provider.ToLowerInvariant();
        if (p.Contains("mgm")) return Book.Mgm;
        if (p.Contains("draftkings")) return Book.DraftKings;
        if (p.Contains("fanduel")) return Book.FanDuel;
        if (p.Contains("pinnacle")) return Book.Pinnacle;
        return Book.Consensus;
    }

    /// <summary>Append TOTAL open/close into an existing warehouse (no wipe).</summary>
    public static async Task ImportTotalsOnlyAsync(string rawRoot, string dbPath, CancellationToken ct = default)
    {
        if (!File.Exists(dbPath))
            throw new FileNotFoundException($"DB not found: {dbPath}. Run import first.");
        var cs = $"Data Source={dbPath}";
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(cs).Options;
        await using var db = new AppDbContext(options);
        var gamesByKey = await db.Games.AsNoTracking()
            .ToDictionaryAsync(g => g.SourceKey, g => g, StringComparer.Ordinal, ct);
        var bSqlite = Path.Combine(rawRoot, "kaggle", "B_basketball-betting-dataset", "basketball-final.sqlite");
        var eCsv = Path.Combine(rawRoot, "kaggle", "E_nba-closing-lines-ats-2024-25", "nba_2024_25_closing_lines_ats.csv");
        await db.OddsSnapshots.Where(o => o.Market == "TOTAL").ExecuteDeleteAsync(ct);
        await ImportTotalsFromBAsync(db, bSqlite, gamesByKey, ct);
        await ImportTotalsFromEAsync(db, eCsv, gamesByKey, ct);
        var n = await db.OddsSnapshots.CountAsync(o => o.Market == "TOTAL", ct);
        Console.WriteLine($"TOTAL snapshots: {n}");
    }

    private static async Task ImportTotalsFromBAsync(
        AppDbContext db, string sqlitePath, Dictionary<string, Game> gamesByKey, CancellationToken ct)
    {
        if (!File.Exists(sqlitePath)) return;
        await using var conn = new SqliteConnection($"Data Source={sqlitePath}");
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT Date, HomeTeam, AwayTeam, Over_AtOpen, Over_AtClose
            FROM BettingOdds_History
            WHERE Over_AtOpen IS NOT NULL AND Over_AtClose IS NOT NULL
            """;
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var snaps = new List<OddsSnapshot>(32768);
        while (await r.ReadAsync(ct))
        {
            if (!TryParseDateOnly(r.GetString(0), out var date)) continue;
            var home = TeamCodes.TryNormalize(r.GetString(1));
            var away = TeamCodes.TryNormalize(r.GetString(2));
            if (home is null || away is null) continue;
            if (!gamesByKey.TryGetValue(TeamCodes.GameKey(date, home, away), out var g)) continue;
            if (r.IsDBNull(3) || r.IsDBNull(4)) continue;
            var open = r.GetDouble(3);
            var close = r.GetDouble(4);
            snaps.Add(Total(g.Id, Book.Consensus, open, -110, PriceKind.Open, g.TipoffUtc.AddHours(-24)));
            snaps.Add(Total(g.Id, Book.Consensus, close, -110, PriceKind.Close, g.TipoffUtc));
        }

        db.OddsSnapshots.AddRange(snaps);
        await db.SaveChangesAsync(ct);
    }

    private static async Task ImportTotalsFromEAsync(
        AppDbContext db, string path, Dictionary<string, Game> gamesByKey, CancellationToken ct)
    {
        if (!File.Exists(path)) return;
        using var reader = new StreamReader(path);
        var header = await reader.ReadLineAsync(ct);
        if (header is null) return;
        var cols = SplitCsv(header);
        var iDate = Idx(cols, "date_et");
        var iHome = Idx(cols, "home_team");
        var iAway = Idx(cols, "away_team");
        var iOpen = Idx(cols, "open_total");
        var iClose = Idx(cols, "close_total");
        var snaps = new List<OddsSnapshot>();
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            var c = SplitCsv(line);
            if (c.Length <= iClose) continue;
            var home = TeamCodes.TryNormalize(c[iHome]);
            var away = TeamCodes.TryNormalize(c[iAway]);
            if (home is null || away is null) continue;
            var datePart = c[iDate].Length >= 10 ? c[iDate][..10] : c[iDate];
            if (!DateOnly.TryParse(datePart, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                continue;
            if (!gamesByKey.TryGetValue(TeamCodes.GameKey(date, home, away), out var g)) continue;
            if (!TryParseDouble(c[iOpen], out var open) || !TryParseDouble(c[iClose], out var close)) continue;
            snaps.Add(Total(g.Id, Book.Consensus, open, -110, PriceKind.Open, g.TipoffUtc.AddHours(-24)));
            snaps.Add(Total(g.Id, Book.Consensus, close, -110, PriceKind.Close, g.TipoffUtc));
        }

        db.OddsSnapshots.AddRange(snaps);
        await db.SaveChangesAsync(ct);
    }

    private static OddsSnapshot Total(
        int gameId, Book book, double line, int juice, PriceKind kind, DateTimeOffset at)
        => new()
        {
            GameId = gameId,
            Book = book,
            Market = "TOTAL",
            Side = "over", // line is the total; under = same line other side
            Line = line,
            AmericanOdds = juice,
            PriceKind = kind,
            ClvQuality = ClvQuality.OpenClose,
            AssumedJuice = true,
            CapturedAt = at,
        };

    private static async Task ImportSpreadsFromBAsync(
        AppDbContext db, string sqlitePath, Dictionary<string, Game> gamesByKey, CancellationToken ct)
    {
        await using var conn = new SqliteConnection($"Data Source={sqlitePath}");
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT Date, HomeTeam, AwayTeam, HomeSpread_AtOpen, HomeSpread_AtClose
            FROM BettingOdds_History
            WHERE HomeSpread_AtOpen IS NOT NULL AND HomeSpread_AtClose IS NOT NULL
            """;
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var snaps = new List<OddsSnapshot>(32768);
        while (await r.ReadAsync(ct))
        {
            if (!TryParseDateOnly(r.GetString(0), out var date)) continue;
            var home = TeamCodes.TryNormalize(r.GetString(1));
            var away = TeamCodes.TryNormalize(r.GetString(2));
            if (home is null || away is null) continue;
            if (!gamesByKey.TryGetValue(TeamCodes.GameKey(date, home, away), out var g)) continue;
            if (r.IsDBNull(3) || r.IsDBNull(4)) continue;
            var open = r.GetDouble(3);
            var close = r.GetDouble(4);
            snaps.Add(Spread(g.Id, Book.Consensus, "home", open, -110, PriceKind.Open, ClvQuality.OpenClose, g.TipoffUtc.AddHours(-24), assumedJuice: true));
            snaps.Add(Spread(g.Id, Book.Consensus, "away", -open, -110, PriceKind.Open, ClvQuality.OpenClose, g.TipoffUtc.AddHours(-24), assumedJuice: true));
            snaps.Add(Spread(g.Id, Book.Consensus, "home", close, -110, PriceKind.Close, ClvQuality.OpenClose, g.TipoffUtc, assumedJuice: true));
            snaps.Add(Spread(g.Id, Book.Consensus, "away", -close, -110, PriceKind.Close, ClvQuality.OpenClose, g.TipoffUtc, assumedJuice: true));
        }

        db.OddsSnapshots.AddRange(snaps);
        await db.SaveChangesAsync(ct);
    }

    private static async Task ImportSpreadsFromEspnAsync(
        AppDbContext db, string path, Dictionary<string, Game> gamesByKey, CancellationToken ct)
    {
        if (!File.Exists(path)) return;
        using var reader = new StreamReader(path);
        var header = await reader.ReadLineAsync(ct);
        if (header is null) return;
        var cols = SplitCsv(header);
        var iDate = Idx(cols, "date");
        var iHome = Idx(cols, "home");
        var iAway = Idx(cols, "away");
        var iProv = Idx(cols, "provider");
        var iHo = Idx(cols, "home_spread_open");
        var iHc = Idx(cols, "home_spread_close");
        var iAo = Idx(cols, "away_spread_open");
        var iAc = Idx(cols, "away_spread_close");
        var iJho = Idx(cols, "home_juice_open");
        var iJhc = Idx(cols, "home_juice_close");
        var iJao = Idx(cols, "away_juice_open");
        var iJac = Idx(cols, "away_juice_close");

        var snaps = new List<OddsSnapshot>();
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            var c = SplitCsv(line);
            if (c.Length <= iJac) continue;
            var home = TeamCodes.TryNormalize(c[iHome]);
            var away = TeamCodes.TryNormalize(c[iAway]);
            if (home is null || away is null) continue;
            if (!DateOnly.TryParse(c[iDate], CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                continue;
            if (!gamesByKey.TryGetValue(TeamCodes.GameKey(date, home, away), out var g)) continue;

            if (!TryParseDouble(c[iHo], out var ho) || !TryParseDouble(c[iHc], out var hc)) continue;
            var ao = TryParseDouble(c[iAo], out var aoV) ? aoV : -ho;
            var ac = TryParseDouble(c[iAc], out var acV) ? acV : -hc;
            var jho = ParseAmerican(c[iJho]) ?? -110;
            var jhc = ParseAmerican(c[iJhc]) ?? -110;
            var jao = ParseAmerican(c[iJao]) ?? -110;
            var jac = ParseAmerican(c[iJac]) ?? -110;
            var book = MapEspnBook(c[iProv]);
            var assumed = c[iJho].Length == 0;
            snaps.Add(Spread(g.Id, book, "home", ho, jho, PriceKind.Open, ClvQuality.OpenClose, g.TipoffUtc.AddHours(-24), assumed));
            snaps.Add(Spread(g.Id, book, "away", ao, jao, PriceKind.Open, ClvQuality.OpenClose, g.TipoffUtc.AddHours(-24), assumed));
            snaps.Add(Spread(g.Id, book, "home", hc, jhc, PriceKind.Close, ClvQuality.OpenClose, g.TipoffUtc, assumed));
            snaps.Add(Spread(g.Id, book, "away", ac, jac, PriceKind.Close, ClvQuality.OpenClose, g.TipoffUtc, assumed));
        }

        db.OddsSnapshots.AddRange(snaps);
        await db.SaveChangesAsync(ct);
    }

    private static bool TryParseDouble(string raw, out double v)
        => double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out v);

    private static OddsSnapshot Spread(
        int gameId, Book book, string side, double line, int juice, PriceKind kind, ClvQuality quality,
        DateTimeOffset at, bool assumedJuice)
        => new()
        {
            GameId = gameId,
            Book = book,
            Market = "SPREAD",
            Side = side,
            Line = line,
            AmericanOdds = juice,
            PriceKind = kind,
            ClvQuality = quality,
            AssumedJuice = assumedJuice,
            CapturedAt = at,
        };

    private static async Task ImportRefsFromBAsync(
        AppDbContext db, string sqlitePath, Dictionary<string, Game> gamesByKey, CancellationToken ct)
    {
        var already = (await db.RefereeAssignments.Select(r => r.GameId).Distinct().ToListAsync(ct))
            .ToHashSet();

        await using var conn = new SqliteConnection($"Data Source={sqlitePath}");
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        // Game table covers Donaghy years; odds join alone misses many.
        cmd.CommandText = """
            SELECT o.FIRST_NAME, o.LAST_NAME, g.GAME_DATE_HOME, g.TEAM_ABBREVIATION_HOME, g.TEAM_ABBREVIATION_AWAY
            FROM Game_Officials o
            INNER JOIN Game g ON g.GAME_ID = o.GAME_ID
            """;
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var refs = new List<RefereeAssignment>(8192);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        while (await r.ReadAsync(ct))
        {
            var name = $"{r.GetString(0)} {r.GetString(1)}".Trim();
            if (!TryParseDateOnly(r.GetString(2), out var date))
                continue;
            var home = TeamCodes.TryNormalize(r.GetString(3));
            var away = TeamCodes.TryNormalize(r.GetString(4));
            if (home is null || away is null) continue;
            if (!gamesByKey.TryGetValue(TeamCodes.GameKey(date, home, away), out var g)) continue;
            if (already.Contains(g.Id)) continue;
            var dedupe = $"{g.Id}|{name}";
            if (!seen.Add(dedupe)) continue;
            refs.Add(new RefereeAssignment
            {
                GameId = g.Id,
                OfficialName = name,
                Role = "Referee",
                DonaghyExcludeFromFit = TeamCodes.IsDonaghy(name),
            });
        }

        db.RefereeAssignments.AddRange(refs);
        await db.SaveChangesAsync(ct);
    }

    private static async Task ImportRefsFromDAsync(
        AppDbContext db, string officialsDir, Dictionary<string, Game> gamesByKey, CancellationToken ct)
    {
        if (!Directory.Exists(officialsDir)) return;

        var byEspn = gamesByKey.Values
            .Where(g => !string.IsNullOrEmpty(g.EspnGameId))
            .GroupBy(g => g.EspnGameId!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        var existing = (await db.RefereeAssignments.Select(r => r.GameId + "|" + r.OfficialName).ToListAsync(ct))
            .ToHashSet(StringComparer.Ordinal);

        var refs = new List<RefereeAssignment>();
        foreach (var file in Directory.EnumerateFiles(officialsDir, "officials_*.csv"))
        {
            using var reader = new StreamReader(file);
            var header = await reader.ReadLineAsync(ct);
            if (header is null) continue;
            var cols = SplitCsv(header);
            var iGame = Idx(cols, "game_id");
            var iName = Idx(cols, "official_full_name");
            var iPos = Idx(cols, "official_position");
            while (await reader.ReadLineAsync(ct) is { } line)
            {
                var c = SplitCsv(line);
                if (c.Length <= Math.Max(iGame, iName)) continue;
                if (!byEspn.TryGetValue(c[iGame], out var g)) continue;
                var name = c[iName].Trim();
                if (name.Length == 0) continue;
                var dedupe = $"{g.Id}|{name}";
                if (!existing.Add(dedupe)) continue;
                refs.Add(new RefereeAssignment
                {
                    GameId = g.Id,
                    OfficialName = name,
                    Role = c.Length > iPos ? c[iPos] : null,
                    DonaghyExcludeFromFit = TeamCodes.IsDonaghy(name),
                });
            }
        }

        db.RefereeAssignments.AddRange(refs);
        await db.SaveChangesAsync(ct);
    }

    private static async Task<Gate0ImportReport> BuildReportAsync(AppDbContext db, CancellationToken ct)
    {
        var games = await db.Games.CountAsync(ct);
        var odds = await db.OddsSnapshots.CountAsync(ct);
        var oc = await db.OddsSnapshots.CountAsync(o => o.ClvQuality == ClvQuality.OpenClose, ct);
        var co = await db.OddsSnapshots.CountAsync(o => o.ClvQuality == ClvQuality.CloseOnly, ct);
        var refs = await db.RefereeAssignments.CountAsync(ct);
        var don = await db.RefereeAssignments.CountAsync(r => r.DonaghyExcludeFromFit, ct);
        var holdout = await db.Games.Where(g => g.Season == "2024").ToListAsync(ct);
        var holdoutIds = holdout.Select(g => g.Id).ToHashSet();
        var holdoutMl = await db.OddsSnapshots
            .Where(o => holdoutIds.Contains(o.GameId) && o.Market == "ML" && o.PriceKind == PriceKind.Close && o.Side == "home")
            .Select(o => o.GameId)
            .Distinct()
            .CountAsync(ct);
        var holdoutRefs = await db.RefereeAssignments
            .Where(r => holdoutIds.Contains(r.GameId))
            .Select(r => r.GameId)
            .Distinct()
            .CountAsync(ct);
        var espn = await db.Games.CountAsync(g => g.EspnGameId != null, ct);
        var assumed = await db.OddsSnapshots.CountAsync(o => o.AssumedJuice, ct);

        return new Gate0ImportReport
        {
            Games = games,
            Odds = odds,
            OddsOpenClose = oc,
            OddsCloseOnly = co,
            OddsAssumedJuice = assumed,
            Refs = refs,
            DonaghyFlagged = don,
            Holdout2024Games = holdout.Count,
            Holdout2024WithMlClose = holdoutMl,
            Holdout2024WithRefs = holdoutRefs,
            EspnIdsAttached = espn,
        };
    }

    private static OddsSnapshot Ml(
        int gameId, Book book, string side, int american, PriceKind kind, ClvQuality quality, DateTimeOffset at)
        => new()
        {
            GameId = gameId,
            Book = book,
            Market = "ML",
            Side = side,
            AmericanOdds = american,
            PriceKind = kind,
            ClvQuality = quality,
            CapturedAt = at,
        };

    private static bool TryParseDateOnly(string raw, out DateOnly date)
    {
        // Avoid DateTimeKind.Utc → local conversion shifting the calendar day.
        var s = raw.Trim();
        if (s.Length >= 10 && DateOnly.TryParse(s.AsSpan(0, 10), CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            return true;
        date = default;
        return false;
    }

    private static int? ParseAmerican(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        raw = raw.Trim();
        if (raw is "nan" or "None" or "null") return null;
        if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
            return (int)Math.Round(d);
        return null;
    }

    private static int? ParseInt(string? raw)
        => int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;

    private static int Idx(string[] cols, string name)
    {
        for (var i = 0; i < cols.Length; i++)
            if (string.Equals(cols[i], name, StringComparison.OrdinalIgnoreCase))
                return i;
        throw new InvalidDataException($"Column '{name}' not found. Have: {string.Join(',', cols.Take(12))}...");
    }

    /// <summary>Yields CSV records, joining physical lines while inside quotes.</summary>
    private static IEnumerable<string[]> ReadCsvRecords(string path)
    {
        using var reader = new StreamReader(path);
        var buf = new StringBuilder();
        var inQuotes = false;
        while (reader.ReadLine() is { } line)
        {
            if (buf.Length > 0)
                buf.Append('\n');
            buf.Append(line);
            foreach (var ch in line)
            {
                if (ch == '"')
                    inQuotes = !inQuotes;
            }

            if (!inQuotes)
            {
                yield return SplitCsv(buf.ToString());
                buf.Clear();
            }
        }

        if (buf.Length > 0)
            yield return SplitCsv(buf.ToString());
    }

    private static string[] SplitCsv(string line)
    {
        var list = new List<string>();
        var sb = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    sb.Append('"');
                    i++;
                    continue;
                }

                inQuotes = !inQuotes;
                continue;
            }
            if (ch == ',' && !inQuotes)
            {
                list.Add(sb.ToString());
                sb.Clear();
                continue;
            }
            sb.Append(ch);
        }
        list.Add(sb.ToString());
        return list.ToArray();
    }
}
