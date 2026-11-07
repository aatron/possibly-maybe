using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PossiblyMaybe.Cli.Variants;
using PossiblyMaybe.Data;
using PossiblyMaybe.Domain;
using PossiblyMaybe.Domain.SportPacks;

namespace PossiblyMaybe.Cli.Paper;

/// <summary>
/// Live soft-spot slate: NBA PaceEff + soccer (paper HOLD-PASS + shadow FAIL leagues)
/// + NHL shadow-only (C6; Tier A gated on O→C).
/// Live soft-spot slate: NBA PaceEff + NCAAB (shadow) + HOLD-PASS soccer (MLS / Ligue1 / LigaMX / SerieA).
/// Each game → one <see cref="SlateCandidate"/>; champ GO print uses V0; all variants go to shadow ledger.
/// Live soft-spot slate: NBA PaceEff + soccer (paper HOLD-PASS + shadow leagues).
/// Each game → one <see cref="SlateCandidate"/>; champ GO print uses V0 for paper only; all variants go to shadow ledger.
/// </summary>
public static class DailySlate
{
    private static readonly HttpClient Http = new()
    {
        DefaultRequestHeaders = { { "User-Agent", "PossiblyMaybe/1.0" } },
        Timeout = TimeSpan.FromSeconds(30),
    };

    /// <summary>Paper HOLD-PASS soccer — emit paper-go lines.</summary>
    public static readonly HashSet<string> PaperSoccerSports = new(StringComparer.OrdinalIgnoreCase)
    {
        "MLS", "LIGUE1", "LIGAMX", "SERIEA",
    };

    /// <summary>ESPN soccer league path for paper + shadow sports (VariantShadow iterates Keys).</summary>
    public static readonly IReadOnlyDictionary<string, string> SoccerEspnPaths =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["MLS"] = "usa.1",
            ["LIGUE1"] = "fra.1",
            ["LIGAMX"] = "mex.1",
            ["SERIEA"] = "ita.1",
            ["LALIGA"] = "esp.1",
            ["EFL"] = "eng.2",
            ["BUNDESLIGA"] = "ger.1",
            ["EPL"] = "eng.1",
        };

    public const int StaleDays = 10;

    /// <summary>True when the newest scored game is more than <see cref="StaleDays"/> before the slate date (or missing).</summary>
    public static bool IsStale(DateTimeOffset? latestResult, string yyyymmdd)
        => latestResult is null
           || DateTime.ParseExact(yyyymmdd, "yyyyMMdd", null).Date - latestResult.Value.UtcDateTime.Date > TimeSpan.FromDays(StaleDays);

    private static async Task<bool> IsLeagueStaleAsync(string sport, string dbPath, string yyyymmdd, CancellationToken ct)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={dbPath}").Options;
        await using var db = new AppDbContext(options);
        var latest = await Import.ResultsRefresh.LatestResultAsync(db, sport, ct);
        return IsStale(latest, yyyymmdd);
    }

    private static async Task WarnIfStaleAsync(string sport, string dbPath, string yyyymmdd, CancellationToken ct)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={dbPath}").Options;
        await using var db = new AppDbContext(options);
        var latest = await Import.ResultsRefresh.LatestResultAsync(db, sport, ct);
        if (!IsStale(latest, yyyymmdd)) return;
        Console.WriteLine($"STALE DATA: {sport} newest result in DB is {(latest is null ? "NONE" : latest.Value.ToString("yyyy-MM-dd"))}"
            + $" (> {StaleDays} days before {yyyymmdd}) — NO GOs for {sport}. Run: refresh-results");
    }

    public static async Task RunNbaAsync(string dbPath, string yyyymmdd, double bankroll, CancellationToken ct = default)
    {
        var candidates = await BuildNbaCandidatesAsync(dbPath, yyyymmdd, ct);
        Console.WriteLine($"######## NBA SLATE {yyyymmdd} bankroll=${bankroll:F0} PaceEff+Normalσ ########");
        if (candidates.Count > 0 && candidates[0].Stale)
            await WarnIfStaleAsync("NBA", dbPath, yyyymmdd, ct);
        else
            Console.WriteLine($"Gate: w={ChampConfig.MarketWeight} |exp−line|≥{ChampConfig.MinAbsTotalEdge} EV≥{ChampConfig.MinEv:P0} ¼Kelly≤2%");

        PrintChampGos(candidates, bankroll);
        await RecordShadowAsync(dbPath, candidates, ct);
    }

    /// <summary>NCAAB totals slate — shadow-capable; paper GO only when data is fresh.</summary>
    public static async Task RunNcaabAsync(string dbPath, string yyyymmdd, double bankroll, CancellationToken ct = default)
    {
        List<SlateCandidate> candidates;
        try
        {
            candidates = await BuildNcaabCandidatesAsync(dbPath, yyyymmdd, ct);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"######## NCAAB SLATE {yyyymmdd} ########");
            Console.WriteLine($"Scoreboard fetch failed: {ex.Message}");
            return;
        }

        var pack = SportPackRegistry.Ncaab;
        Console.WriteLine($"######## NCAAB SLATE {yyyymmdd} bankroll=${bankroll:F0} shadow {pack.Model} ########");
        if (candidates.Count == 0)
        {
            Console.WriteLine("No NCAAB games on this date.");
            return;
        }
        if (candidates[0].Stale)
            await WarnIfStaleAsync("NCAAB", dbPath, yyyymmdd, ct);
        else
            Console.WriteLine($"Gate: w={pack.MarketWeight} |exp−line|≥{pack.MinAbsTotalEdge} EV≥{pack.MinEv:P0}");

        PrintChampGos(candidates, bankroll);
        await RecordShadowAsync(dbPath, candidates, ct);
    }

    public static Task RunMlsAsync(string dbPath, string yyyymmdd, double bankroll, CancellationToken ct = default)
        => RunSoccerAsync("MLS", dbPath, yyyymmdd, bankroll, ct);

    /// <summary>Soccer totals: paper HOLD-PASS (MLS/LIGUE1/LIGAMX/SERIEA) or shadow (LALIGA/EFL/EPL/BUNDESLIGA).</summary>
    public static async Task RunSoccerAsync(
        string sport, string dbPath, string yyyymmdd, double bankroll, CancellationToken ct = default)
    {
        sport = sport.ToUpperInvariant();
        if (!SoccerEspnPaths.ContainsKey(sport))
            throw new ArgumentException(
                $"No live slate for {sport}. Paper: MLS, LIGUE1, LIGAMX, SERIEA. Shadow: LALIGA, EFL, EPL, BUNDESLIGA.");

        List<SlateCandidate> candidates;
        try
        {
            candidates = await BuildSoccerCandidatesAsync(sport, dbPath, yyyymmdd, ct);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"######## {sport} SLATE {yyyymmdd} ########");
            Console.WriteLine($"Scoreboard fetch failed: {ex.Message}");
            return;
        }

        var paper = PaperSoccerSports.Contains(sport);
        var modelName = sport switch
        {
            "MLS" => SportPackRegistry.Mls.Model,
            "LIGUE1" => Ligue1ChampConfig.Model,
            "LIGAMX" => LigaMxChampConfig.Model,
            "SERIEA" => SerieAChampConfig.Model,
            "LALIGA" => SportPackRegistry.LaLiga.Model,
            "EFL" => SportPackRegistry.Efl.Model,
            "BUNDESLIGA" => SportPackRegistry.Bundesliga.Model,
            "EPL" => SportPackRegistry.Epl.Model,
            _ => sport,
        };
        var lane = paper ? "paper" : "shadow";
        Console.WriteLine($"######## {sport} SLATE {yyyymmdd} bankroll=${bankroll:F0} {lane} {modelName} ########");
        if (candidates.Count == 0)
        {
            Console.WriteLine($"No {sport} games on this date.");
            return;
        }
        if (candidates[0].Stale)
            await WarnIfStaleAsync(sport, dbPath, yyyymmdd, ct);
        else
        {
            var w = candidates[0].MarketWeight;
            var minEdge = candidates[0].MinAbsEdge;
            var minEv = candidates[0].MinEv;
            Console.WriteLine($"Gate: w={w} |exp−line|≥{minEdge} EV≥{minEv:P0} p={(SoccerOverProbConfig.UsePoissonCdfFor(sport) ? "poisson" : "logistic")}");
        }

        if (paper)
            PrintChampGos(candidates, bankroll);
        else
            Console.WriteLine($"SHADOW-ONLY {sport} (FAIL champ) — {candidates.Count} candidates → ledger (no paper-go).");
            Console.WriteLine($"SHADOW-ONLY {sport} (pre-Tier-A) — {candidates.Count} candidates → ledger (no paper-go).");
        await RecordShadowAsync(dbPath, candidates, ct);
    }

    /// <summary>NHL totals shadow-only (no paper-go). Candidates still hit the variant ledger.</summary>
    public static async Task RunNhlAsync(
        string dbPath, string yyyymmdd, double bankroll, CancellationToken ct = default)
    {
        List<SlateCandidate> candidates;
        try
        {
            candidates = await BuildNhlCandidatesAsync(dbPath, yyyymmdd, ct);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"######## NHL SLATE {yyyymmdd} ########");
            Console.WriteLine($"Scoreboard fetch failed: {ex.Message}");
            return;
        }

        var pack = SportPackRegistry.Nhl;
        Console.WriteLine($"######## NHL SLATE {yyyymmdd} bankroll=${bankroll:F0} shadow {pack.Model} ########");
        if (candidates.Count == 0)
        {
            Console.WriteLine("No NHL games on this date.");
            return;
        }
        if (candidates[0].Stale)
            await WarnIfStaleAsync("NHL", dbPath, yyyymmdd, ct);
        else
            Console.WriteLine($"Gate: w={pack.MarketWeight} |exp−line|≥{pack.MinAbsTotalEdge} EV≥{pack.MinEv:P0} (shadow-only; TierAReady={NhlSportPack.TierAReady})");

        Console.WriteLine($"SHADOW-ONLY NHL (Tier A → radar only; no paper-go) — {candidates.Count} candidates → ledger.");
        await RecordShadowAsync(dbPath, candidates, ct);
    }

    public static async Task<List<SlateCandidate>> BuildNbaCandidatesAsync(
        string dbPath, string yyyymmdd, CancellationToken ct = default)
    {
        var asOf = PointInTime.SlateAsOf(yyyymmdd);
        var (pace, sigma) = await BuildNbaPaceAndSigmaAsync(dbPath, asOf, ct);
        var games = await FetchNbaScoreboardAsync(yyyymmdd, ct);
        var stale = games.Count > 0 && await IsLeagueStaleAsync("NBA", dbPath, yyyymmdd, ct);
        var list = new List<SlateCandidate>();
        foreach (var g in games)
        {
            var modelExp = pace.ExpectedTotal(g.Home, g.Away);
            var odds = g.Total is { } t
                ? new EspnOdds(t, null, null)
                : await FetchEspnOddsAsync("basketball", "nba", g.EspnId, ct);
            list.Add(new SlateCandidate(
                "NBA", yyyymmdd, g.Home, g.Away,
                modelExp, null, null,
                odds?.Total, odds?.Total, // live open≈current until tracker import
                odds?.OverAmerican, odds?.UnderAmerican,
                sigma,
                ChampConfig.MarketWeight, ChampConfig.MinAbsTotalEdge, ChampConfig.MinEv,
                stale));
        }
        return list;
    }

    public static async Task<List<SlateCandidate>> BuildNcaabCandidatesAsync(
        string dbPath, string yyyymmdd, CancellationToken ct = default)
    {
        var pack = SportPackRegistry.Ncaab;
        var asOf = PointInTime.SlateAsOf(yyyymmdd);
        var book = await BuildNcaabScoringBookAsync(dbPath, asOf, ct);
        var games = await FetchNcaabScoreboardAsync(yyyymmdd, ct);
        var stale = games.Count > 0 && await IsLeagueStaleAsync("NCAAB", dbPath, yyyymmdd, ct);
        var list = new List<SlateCandidate>();
        foreach (var g in games)
        {
            var modelExp = book.ExpectedTotal(g.Home, g.Away);
            var odds = g.Total is { } t
                ? new EspnOdds(t, null, null)
                : await FetchEspnOddsAsync("basketball", "mens-college-basketball", g.EspnId, ct);
            list.Add(new SlateCandidate(
                "NCAAB", yyyymmdd, g.Home, g.Away,
                modelExp, null, null,
                odds?.Total, odds?.Total,
                odds?.OverAmerican, odds?.UnderAmerican,
                0,
                pack.MarketWeight, pack.MinAbsTotalEdge, pack.MinEv,
                stale));
        }
        return list;
    }

    public static async Task<List<SlateCandidate>> BuildSoccerCandidatesAsync(
        string sport, string dbPath, string yyyymmdd, CancellationToken ct = default)
    {
        sport = sport.ToUpperInvariant();
        if (!SoccerEspnPaths.TryGetValue(sport, out var espnLeague))
            throw new ArgumentException($"No live slate for {sport}.");

        var asOf = PointInTime.SlateAsOf(yyyymmdd);
        var (_, w, minEdge, minEv, lambdas) = await BuildSoccerModelAsync(sport, dbPath, asOf, ct);
        var games = await FetchSoccerScoreboardAsync(sport, espnLeague, yyyymmdd, ct);
        var stale = games.Count > 0 && await IsLeagueStaleAsync(sport, dbPath, yyyymmdd, ct);
        var list = new List<SlateCandidate>();
        foreach (var g in games)
        {
            if (g.Home.StartsWith('?') || g.Away.StartsWith('?'))
            {
                list.Add(new SlateCandidate(
                    sport, yyyymmdd, g.Home, g.Away,
                    0, null, null, null, null, null, null, 0, w, minEdge, minEv, stale));
                continue;
            }
            var (lh, la) = lambdas(g.Home, g.Away);
            var modelExp = lh + la;
            var odds = g.Total is { } t
                ? new EspnOdds(t, null, null)
                : await FetchEspnOddsAsync("soccer", espnLeague, g.EspnId, ct);
            list.Add(new SlateCandidate(
                sport, yyyymmdd, g.Home, g.Away,
                modelExp, lh, la,
                odds?.Total, odds?.Total,
                odds?.OverAmerican, odds?.UnderAmerican,
                0, w, minEdge, minEv, stale));
        }
        return list;
    }

    public static async Task<List<SlateCandidate>> BuildNhlCandidatesAsync(
        string dbPath, string yyyymmdd, CancellationToken ct = default)
    {
        var pack = SportPackRegistry.Nhl;
        var asOf = PointInTime.SlateAsOf(yyyymmdd);
        var lambdas = await BuildNhlModelAsync(dbPath, asOf, ct);
        var games = await FetchNhlScoreboardAsync(yyyymmdd, ct);
        var stale = games.Count > 0 && await IsLeagueStaleAsync("NHL", dbPath, yyyymmdd, ct);
        var list = new List<SlateCandidate>();
        foreach (var g in games)
        {
            if (g.Home.StartsWith('?') || g.Away.StartsWith('?'))
            {
                list.Add(new SlateCandidate(
                    "NHL", yyyymmdd, g.Home, g.Away,
                    0, null, null, null, null, null, null, 0,
                    pack.MarketWeight, pack.MinAbsTotalEdge, pack.MinEv, stale));
                continue;
            }
            var (lh, la) = lambdas(g.Home, g.Away);
            var modelExp = lh + la;
            var odds = g.Total is { } t
                ? new EspnOdds(t, null, null)
                : await FetchEspnOddsAsync("hockey", "nhl", g.EspnId, ct);
            list.Add(new SlateCandidate(
                "NHL", yyyymmdd, g.Home, g.Away,
                modelExp, lh, la,
                odds?.Total, odds?.Total,
                odds?.OverAmerican, odds?.UnderAmerican,
                0, pack.MarketWeight, pack.MinAbsTotalEdge, pack.MinEv, stale));
        }
        return list;
    }

    private static void PrintChampGos(IReadOnlyList<SlateCandidate> candidates, double bankroll)
    {
        var gos = 0;
        var sport = candidates.Count > 0 ? candidates[0].Sport : "";
        // R6: paper GO / paper-go lines use real DK juice when present (V0 stays −110 for shadow).
        var paperV = VariantRegistry.PaperChamp;
        foreach (var c in candidates)
        {
            var d = VariantRegistry.Evaluate(paperV, c);
            var key = VariantRegistry.GameKey(c);
            if (!d.Pick)
            {
                if (d.SkipReason == VariantSkip.Excluded)
                    Console.WriteLine($"EXCLUDED (Oregon rule) {c.Away}@{c.Home}");
                else if (d.SkipReason == VariantSkip.Stale)
                    continue; // header already warned
                else if (d.SkipReason == VariantSkip.NoLine)
                    Console.WriteLine($"SKIP {c.Away}@{c.Home} — no total");
                else if (d.SkipReason == VariantSkip.NoEdge)
                    Console.WriteLine($"NO-GO {c.Away}@{c.Home}  model={c.ModelExp:F1} line={c.Line:F1} edge={(c.Line is { } ln ? c.MarketWeight * ln + (1 - c.MarketWeight) * c.ModelExp - ln : 0):+0.0;-0.0}");
                else if (d.Side is { } side)
                    Console.WriteLine($"NO-GO {c.Away}@{c.Home}  {side} {c.Line:F1} ({d.SkipReason}: EV={d.Ev:P1})");
                else if (c.Home.StartsWith('?') || c.Away.StartsWith('?'))
                    Console.WriteLine($"SKIP {c.Away}@{c.Home} — unmapped ESPN team");
                else
                    Console.WriteLine($"NO-GO {c.Away}@{c.Home}  ({d.SkipReason})");
                continue;
            }

            gos++;
            var juiceTag = d.AssumedPrice ? " assumed" : "";
            Console.WriteLine(
                $"GO  {key}  {d.Side} {d.Line:F1} @{d.AmericanOdds}{juiceTag}  model={c.ModelExp:F1} edge={d.AbsEdge:F1} EV={d.Ev:P1} stake=${BetDecider.Advise(d.ModelProb, d.AmericanOdds, bankroll, c.MinEv).StakeFraction * bankroll:F2}");
            Console.WriteLine(
                $"    paper-go \"{key}\" {d.Side} {d.Line} {d.AmericanOdds} {d.ModelProb:F3} {d.AbsEdge:F2} {c.Sport}{(d.AssumedPrice ? " assumed" : "")}");
        }

        Console.WriteLine(gos == 0
            ? (sport == "NBA"
                ? "No NBA soft spots today — sit out. That is a win for bankroll discipline."
                : $"No {sport} soft spots today — sit out.")
            : $"{sport} GO tickets: {gos}");
    }

    private static async Task RecordShadowAsync(string warehouseDbPath, List<SlateCandidate> candidates, CancellationToken ct)
    {
        var root = FindRepoRootNear(warehouseDbPath);
        var vpath = VariantLedger.DefaultDbPath(root);
        var n = await VariantEvaluate.RecordAsync(vpath, candidates, ct);
        if (n > 0)
            Console.WriteLine($"  [shadow] {n} variant×game rows → {vpath}");
    }

    private static string FindRepoRootNear(string dbPath)
    {
        var dir = new DirectoryInfo(Path.GetDirectoryName(Path.GetFullPath(dbPath)) ?? ".");
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "PossiblyMaybe.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }
        return Directory.GetCurrentDirectory();
    }

    private static async Task<ScoringBook> BuildNcaabScoringBookAsync(
        string dbPath, DateTimeOffset? asOf, CancellationToken ct)
    {
        var pack = SportPackRegistry.Ncaab;
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={dbPath}").Options;
        await using var db = new AppDbContext(options);
        var games = PointInTime.AsOf(
                await db.Games.AsNoTracking()
                    .Where(g => g.Sport == "NCAAB" && g.HomeScore != null)
                    .ToListAsync(ct),
                g => g.TipoffUtc, asOf)
            .OrderBy(g => g.TipoffUtc).ThenBy(g => g.Id)
            .ToList();
        var book = new ScoringBook(alpha: pack.EmaAlpha, leagueInit: NcaabChampConfig.LeagueInitTotal);
        foreach (var g in games)
            book.Update(g.HomeTeam, g.AwayTeam, g.HomeScore!.Value, g.AwayScore!.Value);
        return book;
    }

    private static async Task<(PaceEfficiencyBook Pace, double Sigma)> BuildNbaPaceAndSigmaAsync(
        string dbPath, DateTimeOffset? asOf, CancellationToken ct)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={dbPath}").Options;
        await using var db = new AppDbContext(options);
        var games = PointInTime.AsOf(
                await db.Games.AsNoTracking()
                    .Where(g => g.Sport == "NBA" && g.HomeScore != null)
                    .ToListAsync(ct),
                g => g.TipoffUtc, asOf)
            .OrderBy(g => g.TipoffUtc).ThenBy(g => g.Id)
            .ToList();
        Dictionary<int, GameBox> boxes = [];
        try { boxes = await db.GameBoxes.AsNoTracking().ToDictionaryAsync(b => b.GameId, ct); }
        catch { /* */ }
        var odds = await db.OddsSnapshots.AsNoTracking()
            .Where(o => o.Market == "TOTAL" && o.PriceKind == PriceKind.Open)
            .ToListAsync(ct);
        var openByGame = odds.GroupBy(o => o.GameId).ToDictionary(g => g.Key, g => g.First());

        var pace = new PaceEfficiencyBook();
        var resid = new ExpandingResidualSigma();
        foreach (var g in games)
        {
            var modelExp = pace.ExpectedTotal(g.HomeTeam, g.AwayTeam);
            if (openByGame.TryGetValue(g.Id, out var open) && open.Line is { } line
                && g.HomeScore is { } hs && g.AwayScore is { } as_)
            {
                var exp = ChampConfig.MarketWeight * line + (1 - ChampConfig.MarketWeight) * modelExp;
                resid.Observe(hs + as_ - exp);
            }

            if (!boxes.TryGetValue(g.Id, out var b)) continue;
            pace.Update(g.HomeTeam, g.AwayTeam,
                b.FgaHome, b.OrebHome, b.TovHome, b.FtaHome, b.PtsHome, b.Fg3aHome,
                b.FgaAway, b.OrebAway, b.TovAway, b.FtaAway, b.PtsAway, b.Fg3aAway);
        }
        return (pace, resid.Sigma);
    }

    private static async Task<(string ModelName, double W, double MinEdge, double MinEv, Func<string, string, (double Lh, double La)> Lambdas)>
        BuildSoccerModelAsync(string sport, string dbPath, DateTimeOffset? asOf, CancellationToken ct)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={dbPath}").Options;
        await using var db = new AppDbContext(options);
        var games = PointInTime.AsOf(
                await db.Games.AsNoTracking()
                    .Where(g => g.Sport == sport && g.HomeScore != null)
                    .ToListAsync(ct),
                g => g.TipoffUtc, asOf)
            .OrderBy(g => g.TipoffUtc).ThenBy(g => g.Id)
            .ToList();

        if (sport == "MLS")
        {
            Dictionary<int, MlsGameXg> xg = [];
            try { xg = await db.MlsGameXgs.AsNoTracking().ToDictionaryAsync(x => x.GameId, ct); }
            catch { /* */ }
            var pack = SportPackRegistry.Mls;
            var xgBook = new ScoringBook(alpha: pack.EmaAlpha, leagueInit: 2.7);
            var goalsBook = new ScoringBook(alpha: pack.EmaAlpha, leagueInit: 2.7);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var g in games)
            {
                goalsBook.Update(g.HomeTeam, g.AwayTeam, g.HomeScore!.Value, g.AwayScore!.Value);
                if (!xg.TryGetValue(g.Id, out var row)) continue;
                xgBook.Update(g.HomeTeam, g.AwayTeam, row.HomeXg, row.AwayXg);
                seen.Add(g.HomeTeam);
                seen.Add(g.AwayTeam);
            }

            return (pack.Model, pack.MarketWeight, pack.MinAbsTotalEdge,
                pack.MinEv,
                (home, away) => seen.Contains(home) && seen.Contains(away)
                    ? xgBook.ExpectedLambdas(home, away)
                    : goalsBook.ExpectedLambdas(home, away));
        }

        if (sport == "LIGUE1")
        {
            var book = new ScoringBook(alpha: Ligue1ChampConfig.EmaAlpha, leagueInit: 2.7);
            foreach (var g in games)
                book.Update(g.HomeTeam, g.AwayTeam, g.HomeScore!.Value, g.AwayScore!.Value);
            return (Ligue1ChampConfig.Model, Ligue1ChampConfig.MarketWeight, Ligue1ChampConfig.MinAbsTotalEdge,
                Ligue1ChampConfig.MinEv, book.ExpectedLambdas);
        }

        if (sport == "LIGAMX")
        {
            var book = new ScoringBook(alpha: LigaMxChampConfig.EmaAlpha, leagueInit: 2.7);
            foreach (var g in games)
                book.Update(g.HomeTeam, g.AwayTeam, g.HomeScore!.Value, g.AwayScore!.Value);
            return (LigaMxChampConfig.Model, LigaMxChampConfig.MarketWeight, LigaMxChampConfig.MinAbsTotalEdge,
                LigaMxChampConfig.MinEv, book.ExpectedLambdas);
        }

        if (sport == "SERIEA")
        {
            var decisionTime = asOf ?? DateTimeOffset.UtcNow;
            var cutoff = decisionTime.AddYears(-3);
            var dc = new DixonColesBook(alpha: SerieAChampConfig.DcAlpha);
            var goals = new ScoringBook(alpha: SerieAChampConfig.DcAlpha, leagueInit: 2.7);
            foreach (var g in games)
            {
                if (g.TipoffUtc < cutoff) continue;
                dc.Update(g.HomeTeam, g.AwayTeam, g.HomeScore!.Value, g.AwayScore!.Value);
                goals.Update(g.HomeTeam, g.AwayTeam, g.HomeScore!.Value, g.AwayScore!.Value);
            }

            return (SerieAChampConfig.Model, SerieAChampConfig.MarketWeight, SerieAChampConfig.MinAbsTotalEdge,
                SerieAChampConfig.MinEv,
                (home, away) =>
                {
                    var (lh, la) = dc.ExpectedLambdas(home, away);
                    var t = lh + la;
                    return t is >= 1.5 and <= 5.0 ? (lh, la) : goals.ExpectedLambdas(home, away);
                });
        }

        if (sport is "LALIGA" or "EFL" or "EPL" or "BUNDESLIGA")
        {
            var pack = sport switch
            {
                "LALIGA" => SportPackRegistry.LaLiga,
                "EFL" => SportPackRegistry.Efl,
                "EPL" => SportPackRegistry.Epl,
                _ => SportPackRegistry.Bundesliga,
            };
            var book = new ScoringBook(alpha: pack.EmaAlpha, leagueInit: 2.7);
            foreach (var g in games)
                book.Update(g.HomeTeam, g.AwayTeam, g.HomeScore!.Value, g.AwayScore!.Value);
            return (pack.Model, pack.MarketWeight, pack.MinAbsTotalEdge, pack.MinEv, book.ExpectedLambdas);
        }

        throw new ArgumentException($"No champ model for {sport}");
    }

    private static async Task<Func<string, string, (double Lh, double La)>> BuildNhlModelAsync(
        string dbPath, DateTimeOffset? asOf, CancellationToken ct)
    {
        var pack = SportPackRegistry.Nhl;
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={dbPath}").Options;
        await using var db = new AppDbContext(options);
        var games = PointInTime.AsOf(
                await db.Games.AsNoTracking()
                    .Where(g => g.Sport == "NHL" && g.HomeScore != null)
                    .ToListAsync(ct),
                g => g.TipoffUtc, asOf)
            .OrderBy(g => g.TipoffUtc).ThenBy(g => g.Id)
            .ToList();
        var book = new ScoringBook(alpha: pack.EmaAlpha, leagueInit: NhlShadowConfig.LeagueInit);
        foreach (var g in games)
            book.Update(g.HomeTeam, g.AwayTeam, g.HomeScore!.Value, g.AwayScore!.Value);
        return book.ExpectedLambdas;
    }

    private static async Task<List<SlateGame>> FetchNbaScoreboardAsync(string yyyymmdd, CancellationToken ct)
    {
        var url = $"https://site.api.espn.com/apis/site/v2/sports/basketball/nba/scoreboard?dates={yyyymmdd}";
        using var doc = await Http.GetFromJsonAsync<JsonDocument>(url, ct)
            ?? throw new InvalidOperationException("NBA scoreboard empty");
        return ParseScoreboard(doc, "NBA");
    }

    private static async Task<List<SlateGame>> FetchNcaabScoreboardAsync(string yyyymmdd, CancellationToken ct)
    {
        var url =
            $"https://site.api.espn.com/apis/site/v2/sports/basketball/mens-college-basketball/scoreboard?dates={yyyymmdd}";
        using var doc = await Http.GetFromJsonAsync<JsonDocument>(url, ct)
            ?? throw new InvalidOperationException("NCAAB scoreboard empty");
        return ParseScoreboard(doc, "NCAAB");
    }

    private static async Task<List<SlateGame>> FetchSoccerScoreboardAsync(
        string sport, string espnLeague, string yyyymmdd, CancellationToken ct)
    {
        var url = $"https://site.api.espn.com/apis/site/v2/sports/soccer/{espnLeague}/scoreboard?dates={yyyymmdd}";
        using var doc = await Http.GetFromJsonAsync<JsonDocument>(url, ct)
            ?? throw new InvalidOperationException($"{sport} scoreboard empty");
        return ParseScoreboard(doc, sport);
    }

    private static async Task<List<SlateGame>> FetchNhlScoreboardAsync(string yyyymmdd, CancellationToken ct)
    {
        var url = $"https://site.api.espn.com/apis/site/v2/sports/hockey/nhl/scoreboard?dates={yyyymmdd}";
        using var doc = await Http.GetFromJsonAsync<JsonDocument>(url, ct)
            ?? throw new InvalidOperationException("NHL scoreboard empty");
        return ParseScoreboard(doc, "NHL");
    }

    private static List<SlateGame> ParseScoreboard(JsonDocument doc, string sport)
    {
        var list = new List<SlateGame>();
        if (!doc.RootElement.TryGetProperty("events", out var events)) return list;
        foreach (var e in events.EnumerateArray())
        {
            try
            {
                var id = e.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "";
                if (!e.TryGetProperty("competitions", out var compsArr) || compsArr.ValueKind != JsonValueKind.Array
                    || compsArr.GetArrayLength() == 0)
                    continue;
                var comps = compsArr[0];
                if (comps.ValueKind != JsonValueKind.Object) continue;
                string? home = null, away = null;
                if (!comps.TryGetProperty("competitors", out var competitors)
                    || competitors.ValueKind != JsonValueKind.Array)
                    continue;
                foreach (var c in competitors.EnumerateArray())
                {
                    if (!c.TryGetProperty("team", out var team) || team.ValueKind != JsonValueKind.Object)
                        continue;
                    var abbr = team.TryGetProperty("abbreviation", out var ab) ? ab.GetString() : null;
                    var display = team.TryGetProperty("displayName", out var dn) ? dn.GetString() : null;
                    var shortName = team.TryGetProperty("shortDisplayName", out var sd) ? sd.GetString() : null;
                    var name = ResolveTeam(sport, abbr, display, shortName);
                    var ha = c.TryGetProperty("homeAway", out var haEl) ? haEl.GetString() : null;
                    if (ha == "home") home = name;
                    else if (ha == "away") away = name;
                }
                if (home is null || away is null) continue;
                double? total = null;
                if (comps.TryGetProperty("odds", out var odds) && odds.ValueKind == JsonValueKind.Array)
                {
                    foreach (var o in odds.EnumerateArray())
                    {
                        if (o.ValueKind != JsonValueKind.Object) continue;
                        if (o.TryGetProperty("overUnder", out var ou)
                            && ou.ValueKind is JsonValueKind.Number
                            && ou.TryGetDouble(out var v))
                        {
                            total = v;
                            break;
                        }
                    }
                }
                list.Add(new SlateGame(id, home, away, total));
            }
            catch
            {
                // skip malformed ESPN event
            }
        }
        return list;
    }

    private static string? ResolveTeam(string sport, string? abbr, string? display, string? shortName)
    {
        if (sport == "NBA")
        {
            var n = TeamCodes.TryNormalize(abbr) ?? abbr;
            return n;
        }

        if (sport == "NHL")
            return SportTeams.ResolveNhl(abbr, display, shortName);
        if (sport == "NCAAB")
            return SportTeams.ResolveNcaa(abbr, display, shortName);

        return SportTeams.ResolveSoccer(sport, abbr, display, shortName);
    }

    private static async Task<EspnOdds?> FetchEspnOddsAsync(string sport, string league, string espnId, CancellationToken ct)
    {
        try
        {
            var url =
                $"https://sports.core.api.espn.com/v2/sports/{sport}/leagues/{league}/events/{espnId}/competitions/{espnId}/odds";
            using var doc = await Http.GetFromJsonAsync<JsonDocument>(url, ct);
            if (doc is null || !doc.RootElement.TryGetProperty("items", out var items)) return null;

            EspnOdds? any = null;
            foreach (var it in items.EnumerateArray())
            {
                if (!it.TryGetProperty("$ref", out var pref)) continue;
                var refUrl = pref.GetString()?.Replace("http://", "https://");
                if (refUrl is null) continue;
                using var det = await Http.GetFromJsonAsync<JsonDocument>(refUrl, ct);
                if (det is null) continue;
                var root = det.RootElement;
                if (!root.TryGetProperty("overUnder", out var ou) || !ou.TryGetDouble(out var total))
                    continue;
                var over = ReadAmerican(root, "overOdds");
                var under = ReadAmerican(root, "underOdds");
                var prov = root.TryGetProperty("provider", out var p) && p.TryGetProperty("name", out var n)
                    ? n.GetString() ?? ""
                    : "";
                var odds = new EspnOdds(total, over, under);
                if (prov.Contains("Draft", StringComparison.OrdinalIgnoreCase)
                    || prov.Contains("ESPN BET", StringComparison.OrdinalIgnoreCase)
                    || prov.Contains("FanDuel", StringComparison.OrdinalIgnoreCase))
                    return odds;
                any ??= odds;
            }
            return any;
        }
        catch
        {
            return null;
        }
    }

    private static int? ReadAmerican(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var el)) return null;
        if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var i)) return i;
        if (el.ValueKind == JsonValueKind.String && int.TryParse(el.GetString(), out var s)) return s;
        return null;
    }

    private readonly record struct SlateGame(string EspnId, string Home, string Away, double? Total);
    private readonly record struct EspnOdds(double Total, int? OverAmerican, int? UnderAmerican);
}
