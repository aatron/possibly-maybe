using Microsoft.EntityFrameworkCore;
using PossiblyMaybe.Data;
using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Cli.Backtest;

/// <summary>
/// Replay HOLD-PASS soft-spot tickets chronologically through stake schemes ($1000 seed, no reload).
/// Venue juice scenarios stand in for Robinhood/Crypto.com where we lack historical PM asks.
/// </summary>
public static class StakeLaneHoldoutSim
{
    public readonly record struct Bet(
        DateTimeOffset Tip,
        string Sport,
        double ModelProb,
        double AbsEdge,
        bool Won);

    public static async Task RunAsync(string dbPath, CancellationToken ct = default)
    {
        Console.WriteLine("######## Stake-lane holdout sim ($1000 seed, no reload) ########");
        Console.WriteLine("Same PASS tickets as reports/live_kelly_holdout.md (NBA+MLS+Ligue1+LigaMX+SerieA).");
        Console.WriteLine("Robinhood/Crypto.com: no historical asks → juice scenarios on the same tickets.\n");

        var bets = new List<Bet>();
        bets.AddRange(await CollectNbaAsync(dbPath, ct));
        bets.AddRange(await CollectMlsAsync(dbPath, ct));
        bets.AddRange(await CollectSoccerEmaAsync(dbPath, "LIGUE1", "2024-2025",
            Ligue1ChampConfig.MarketWeight, Ligue1ChampConfig.MinAbsTotalEdge, Ligue1ChampConfig.MinEv,
            Ligue1ChampConfig.EmaAlpha, ct));
        bets.AddRange(await CollectSoccerEmaAsync(dbPath, "LIGAMX", "2024-2025",
            LigaMxChampConfig.MarketWeight, LigaMxChampConfig.MinAbsTotalEdge, LigaMxChampConfig.MinEv,
            LigaMxChampConfig.EmaAlpha, ct));
        bets.AddRange(await CollectSoccerDcAsync(dbPath, ct));

        bets = bets.OrderBy(b => b.Tip).ThenBy(b => b.Sport).ToList();
        Console.WriteLine($"Tickets: {bets.Count}  by sport: {string.Join(", ", bets.GroupBy(b => b.Sport).Select(g => $"{g.Key}={g.Count()}"))}");
        Console.WriteLine($"Hit rate: {bets.Count(b => b.Won) / (double)Math.Max(1, bets.Count):P1}\n");

        // DK / warehouse path used −110 (or open juice ≈ −110) in the original sim.
        PrintVenue("DraftKings / warehouse (−110)", bets, -110);

        // Prediction-market fills are usually worse than −110 on the same strike.
        Console.WriteLine("\n--- Robinhood / Crypto.com (same tickets; hypothetical fill juice) ---");
        Console.WriteLine("RH sports ECs ≈ Kalshi; Crypto.com Predict = binary asks. No historical archive → stress juice.");
        foreach (var juice in new[] { -110, -120, -130, -150 })
        {
            var buy = OddsMath.ImpliedProbabilityFromAmerican(juice);
            PrintVenue($"PM fill @{juice} (buy≈{buy:F3})", bets, juice);
        }

        Console.WriteLine("\nNote: −110 PM column is an optimistic ceiling (as-good-as warehouse).");
        Console.WriteLine("Real RH/Crypto fills were likely closer to −120…−150 on many strikes; use those rows.");
    }

    static void PrintVenue(string label, List<Bet> bets, int american)
    {
        Console.WriteLine($"\n{label}");
        Console.WriteLine($"{"Scheme",-12} {"End",8} {"PnL",8} {"bets",6} {"maxDD",8}");
        foreach (var scheme in StakeSchemes.All)
        {
            var (end, pnl, n, dd) = Simulate(scheme.Code, bets, american);
            Console.WriteLine($"{scheme.Code,-12} {end,8:F0} {pnl,8:F0} {n,6} {dd,8:P1}");
        }
    }

    static (double End, double Pnl, int N, double MaxDd) Simulate(string scheme, List<Bet> bets, int american)
    {
        var bank = StakeTrial.SeedDollars;
        var peak = bank;
        var maxDd = 0.0;
        var n = 0;
        foreach (var b in bets)
        {
            var stake = StakeSchemes.StakeDollars(scheme, b.ModelProb, american, bank, b.AbsEdge, b.Sport);
            if (stake < 0.50) continue;
            n++;
            var pnlU = TotalsMath.OuPnL(american, b.Won ? 1 : -1);
            bank += stake * pnlU;
            if (bank <= 0) { bank = 0; break; }
            peak = Math.Max(peak, bank);
            maxDd = Math.Max(maxDd, peak <= 0 ? 0 : (peak - bank) / peak);
        }

        return (bank, bank - StakeTrial.SeedDollars, n, maxDd);
    }

    static async Task<List<Bet>> CollectNbaAsync(string dbPath, CancellationToken ct)
    {
        const double w = 0.5, minEv = 0.04, minEdge = 6.0;
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={dbPath}").Options;
        await using var db = new AppDbContext(options);
        var games = (await db.Games.AsNoTracking()
                .Where(g => g.Sport == "NBA" && g.HomeScore != null && g.AwayScore != null)
                .ToListAsync(ct))
            .OrderBy(g => g.TipoffUtc).ThenBy(g => g.Id).ToList();
        var odds = await db.OddsSnapshots.AsNoTracking().Where(o => o.Market == "TOTAL").ToListAsync(ct);
        var byGame = odds.GroupBy(o => o.GameId).ToDictionary(g => g.Key, g => g.ToList());
        Dictionary<int, GameBox> boxes = [];
        try { boxes = await db.GameBoxes.AsNoTracking().ToDictionaryAsync(b => b.GameId, ct); }
        catch { /* */ }

        var pace = new PaceEfficiencyBook();
        var outBets = new List<Bet>();
        foreach (var g in games)
        {
            byGame.TryGetValue(g.Id, out var snaps);
            snaps ??= [];
            var open = snaps.FirstOrDefault(s => s.PriceKind == PriceKind.Open);
            var close = snaps.FirstOrDefault(s => s.PriceKind == PriceKind.Close);
            var modelExp = pace.ExpectedTotal(g.HomeTeam, g.AwayTeam);

            if (open?.Line is { } line && close?.Line is { } closeLine
                && Math.Abs(line - closeLine) >= 0.25
                && g.Season == TotalsEdgeBacktest.HoldoutSeason)
            {
                var exp = w * line + (1 - w) * modelExp;
                var edge = exp - line;
                var juice = open.AmericanOdds == 0 ? -110 : open.AmericanOdds;
                var ou = TotalsMath.OuResult(g.HomeScore!.Value, g.AwayScore!.Value, line);
                var pOver = TotalsMath.OverProb(exp, line);
                if (ou != 0 && Math.Abs(edge) >= minEdge)
                {
                    if (edge > 0)
                    {
                        var ev = OddsMath.ExpectedValue(pOver, juice);
                        if (ev >= minEv)
                            outBets.Add(new Bet(g.TipoffUtc, "NBA", pOver, Math.Abs(edge), ou > 0));
                    }
                    else
                    {
                        var pUnder = 1 - pOver;
                        var ev = OddsMath.ExpectedValue(pUnder, juice);
                        if (ev >= minEv)
                            outBets.Add(new Bet(g.TipoffUtc, "NBA", pUnder, Math.Abs(edge), ou < 0));
                    }
                }
            }

            if (boxes.TryGetValue(g.Id, out var box))
            {
                pace.Update(g.HomeTeam, g.AwayTeam,
                    box.FgaHome, box.OrebHome, box.TovHome, box.FtaHome, box.PtsHome, box.Fg3aHome,
                    box.FgaAway, box.OrebAway, box.TovAway, box.FtaAway, box.PtsAway, box.Fg3aAway);
            }
        }

        return outBets;
    }

    static async Task<List<Bet>> CollectMlsAsync(string dbPath, CancellationToken ct)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={dbPath}").Options;
        await using var db = new AppDbContext(options);
        var games = (await db.Games.AsNoTracking()
                .Where(g => g.Sport == "MLS" && g.HomeScore != null)
                .ToListAsync(ct))
            .OrderBy(g => g.TipoffUtc).ThenBy(g => g.Id).ToList();
        var odds = await db.OddsSnapshots.AsNoTracking().Where(o => o.Market == "TOTAL").ToListAsync(ct);
        var byOdds = odds.GroupBy(o => o.GameId).ToDictionary(g => g.Key, g => g.ToList());
        Dictionary<int, MlsGameXg> xgByGame = [];
        try { xgByGame = await db.MlsGameXgs.AsNoTracking().ToDictionaryAsync(x => x.GameId, ct); }
        catch { /* */ }

        var xgBook = new ScoringBook(alpha: MlsChampConfig.EmaAlpha, leagueInit: 2.7);
        var goalsBook = new ScoringBook(alpha: MlsChampConfig.EmaAlpha, leagueInit: 2.7);
        var seenXg = new HashSet<string>(StringComparer.Ordinal);
        var outBets = new List<Bet>();
        const double w = MlsChampConfig.MarketWeight, minEdge = MlsChampConfig.MinAbsTotalEdge, minEv = MlsChampConfig.MinEv;

        foreach (var g in games)
        {
            if (g.Season is "2026")
            {
                ApplyMlsUpdate(g, xgByGame, xgBook, goalsBook, seenXg);
                continue;
            }

            byOdds.TryGetValue(g.Id, out var snaps);
            snaps ??= [];
            var open = snaps.FirstOrDefault(s => s.PriceKind == PriceKind.Open);
            var close = snaps.FirstOrDefault(s => s.PriceKind == PriceKind.Close);
            if (open?.Line is not { } line || close?.Line is not { } closeLine
                || Math.Abs(line - closeLine) < 0.25)
            {
                ApplyMlsUpdate(g, xgByGame, xgBook, goalsBook, seenXg);
                continue;
            }

            var useXg = seenXg.Contains(g.HomeTeam) && seenXg.Contains(g.AwayTeam);
            var modelExp = useXg
                ? xgBook.ExpectedTotal(g.HomeTeam, g.AwayTeam)
                : goalsBook.ExpectedTotal(g.HomeTeam, g.AwayTeam);
            var exp = w * line + (1 - w) * modelExp;
            var edge = exp - line;
            var ou = TotalsMath.OuResult(g.HomeScore!.Value, g.AwayScore!.Value, line);
            var pOver = TotalsMath.OverProb(exp, line, sharpness: 1.2);

            if (g.Season == MlsTotalsBacktest.HoldoutSeason && ou != 0 && Math.Abs(edge) >= minEdge)
            {
                if (edge > 0)
                {
                    if (OddsMath.ExpectedValue(pOver, -110) >= minEv)
                        outBets.Add(new Bet(g.TipoffUtc, "MLS", pOver, Math.Abs(edge), ou > 0));
                }
                else
                {
                    var pUnder = 1 - pOver;
                    if (OddsMath.ExpectedValue(pUnder, -110) >= minEv)
                        outBets.Add(new Bet(g.TipoffUtc, "MLS", pUnder, Math.Abs(edge), ou < 0));
                }
            }

            ApplyMlsUpdate(g, xgByGame, xgBook, goalsBook, seenXg);
        }

        return outBets;
    }

    static void ApplyMlsUpdate(
        Game g, Dictionary<int, MlsGameXg> xgByGame, ScoringBook xgBook, ScoringBook goalsBook, HashSet<string> seenXg)
    {
        goalsBook.Update(g.HomeTeam, g.AwayTeam, g.HomeScore!.Value, g.AwayScore!.Value);
        if (!xgByGame.TryGetValue(g.Id, out var row)) return;
        xgBook.Update(g.HomeTeam, g.AwayTeam, row.HomeXg, row.AwayXg);
        seenXg.Add(g.HomeTeam);
        seenXg.Add(g.AwayTeam);
    }

    static async Task<List<Bet>> CollectSoccerEmaAsync(
        string dbPath, string sport, string holdoutSeason,
        double w, double minEdge, double minEv, double alpha, CancellationToken ct)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={dbPath}").Options;
        await using var db = new AppDbContext(options);
        var games = (await db.Games.AsNoTracking()
                .Where(g => g.Sport == sport && g.HomeScore != null)
                .ToListAsync(ct))
            .OrderBy(g => g.TipoffUtc).ThenBy(g => g.Id).ToList();
        var odds = await db.OddsSnapshots.AsNoTracking().Where(o => o.Market == "TOTAL").ToListAsync(ct);
        var byOdds = odds.GroupBy(o => o.GameId).ToDictionary(g => g.Key, g => g.ToList());
        var book = new ScoringBook(alpha: alpha, leagueInit: 2.7);
        var outBets = new List<Bet>();

        foreach (var g in games)
        {
            var season = g.Season ?? "";
            if (string.CompareOrdinal(season, holdoutSeason) > 0)
            {
                book.Update(g.HomeTeam, g.AwayTeam, g.HomeScore!.Value, g.AwayScore!.Value);
                continue;
            }

            byOdds.TryGetValue(g.Id, out var snaps);
            snaps ??= [];
            var open = snaps.FirstOrDefault(s => s.PriceKind == PriceKind.Open);
            var close = snaps.FirstOrDefault(s => s.PriceKind == PriceKind.Close);
            if (open?.Line is not { } line || close?.Line is not { } closeLine
                || Math.Abs(line - closeLine) < 0.25)
            {
                book.Update(g.HomeTeam, g.AwayTeam, g.HomeScore!.Value, g.AwayScore!.Value);
                continue;
            }

            var modelExp = book.ExpectedTotal(g.HomeTeam, g.AwayTeam);
            var exp = w * line + (1 - w) * modelExp;
            var edge = exp - line;
            var ou = TotalsMath.OuResult(g.HomeScore!.Value, g.AwayScore!.Value, line);
            var pOver = TotalsMath.OverProb(exp, line, sharpness: 1.2);

            if (season == holdoutSeason && ou != 0 && Math.Abs(edge) >= minEdge)
            {
                if (edge > 0)
                {
                    if (OddsMath.ExpectedValue(pOver, -110) >= minEv)
                        outBets.Add(new Bet(g.TipoffUtc, sport, pOver, Math.Abs(edge), ou > 0));
                }
                else
                {
                    var pUnder = 1 - pOver;
                    if (OddsMath.ExpectedValue(pUnder, -110) >= minEv)
                        outBets.Add(new Bet(g.TipoffUtc, sport, pUnder, Math.Abs(edge), ou < 0));
                }
            }

            book.Update(g.HomeTeam, g.AwayTeam, g.HomeScore!.Value, g.AwayScore!.Value);
        }

        return outBets;
    }

    static async Task<List<Bet>> CollectSoccerDcAsync(string dbPath, CancellationToken ct)
    {
        const string sport = "SERIEA", holdoutSeason = "2024-2025";
        var w = SerieAChampConfig.MarketWeight;
        var minEdge = SerieAChampConfig.MinAbsTotalEdge;
        var minEv = SerieAChampConfig.MinEv;
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={dbPath}").Options;
        await using var db = new AppDbContext(options);
        var games = (await db.Games.AsNoTracking()
                .Where(g => g.Sport == sport && g.HomeScore != null)
                .ToListAsync(ct))
            .OrderBy(g => g.TipoffUtc).ThenBy(g => g.Id).ToList();
        var odds = await db.OddsSnapshots.AsNoTracking().Where(o => o.Market == "TOTAL").ToListAsync(ct);
        var byOdds = odds.GroupBy(o => o.GameId).ToDictionary(g => g.Key, g => g.ToList());
        var dc = new DixonColesBook(alpha: SerieAChampConfig.DcAlpha);
        var outBets = new List<Bet>();

        foreach (var g in games)
        {
            var season = g.Season ?? "";
            if (string.CompareOrdinal(season, holdoutSeason) > 0)
            {
                dc.Update(g.HomeTeam, g.AwayTeam, g.HomeScore!.Value, g.AwayScore!.Value);
                continue;
            }

            byOdds.TryGetValue(g.Id, out var snaps);
            snaps ??= [];
            var open = snaps.FirstOrDefault(s => s.PriceKind == PriceKind.Open);
            var close = snaps.FirstOrDefault(s => s.PriceKind == PriceKind.Close);
            if (open?.Line is not { } line || close?.Line is not { } closeLine
                || Math.Abs(line - closeLine) < 0.25)
            {
                dc.Update(g.HomeTeam, g.AwayTeam, g.HomeScore!.Value, g.AwayScore!.Value);
                continue;
            }

            var modelExp = dc.ExpectedTotal(g.HomeTeam, g.AwayTeam);
            var exp = w * line + (1 - w) * modelExp;
            var edge = exp - line;
            var ou = TotalsMath.OuResult(g.HomeScore!.Value, g.AwayScore!.Value, line);
            var pOver = TotalsMath.OverProb(exp, line, sharpness: 1.2);

            if (season == holdoutSeason && ou != 0 && Math.Abs(edge) >= minEdge)
            {
                if (edge > 0)
                {
                    if (OddsMath.ExpectedValue(pOver, -110) >= minEv)
                        outBets.Add(new Bet(g.TipoffUtc, sport, pOver, Math.Abs(edge), ou > 0));
                }
                else
                {
                    var pUnder = 1 - pOver;
                    if (OddsMath.ExpectedValue(pUnder, -110) >= minEv)
                        outBets.Add(new Bet(g.TipoffUtc, sport, pUnder, Math.Abs(edge), ou < 0));
                }
            }

            dc.Update(g.HomeTeam, g.AwayTeam, g.HomeScore!.Value, g.AwayScore!.Value);
        }

        return outBets;
    }
}
