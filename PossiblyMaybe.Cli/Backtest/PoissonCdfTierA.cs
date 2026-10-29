using Microsoft.EntityFrameworkCore;
using PossiblyMaybe.Data;
using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Cli.Backtest;

/// <summary>
/// Tier A walk-forward for poisson-cdf-pover: rolling-origin seasons before burned holdout.
/// Compares Poisson CDF vs logistic champ at frozen w/edge/EV. Does not score Tier C.
/// </summary>
public static class PoissonCdfTierA
{
    public static async Task RunAsync(string dbPath, string? sportFilter = null, CancellationToken ct = default)
    {
        Console.WriteLine("######## Tier A: poisson-cdf-pover vs logistic champ ########");
        Console.WriteLine("AssumedJuice −110. Selection = Tier A folds only (no Tier B sort). N_trials=1/league.");
        Console.WriteLine($"SoccerOverProbConfig ForceMode={SoccerOverProbConfig.ForceMode?.ToString() ?? "null (KEEP set)"}\n");

        var sports = sportFilter is null
            ? new[] { "SERIEA", "MLS", "LIGUE1", "LIGAMX" }
            : new[] { sportFilter.ToUpperInvariant() };

        foreach (var sport in sports)
        {
            try { await RunLeagueAsync(dbPath, sport, ct); }
            catch (Exception ex) { Console.WriteLine($"SKIP {sport}: {ex.Message}"); }
        }
    }

    static async Task RunLeagueAsync(string dbPath, string sport, CancellationToken ct)
    {
        var (holdout, w, minEdge, minEv, alpha, useDc, dcAlpha) = Champ(sport);
        var folds = await PreHoldoutSeasonsAsync(dbPath, sport, holdout, ct);
        if (folds.Count == 0)
        {
            Console.WriteLine($"######## {sport} ######## no pre-holdout seasons with odds");
            return;
        }

        Console.WriteLine($"######## {sport} Tier A ######## holdout(burned)={holdout} folds=[{string.Join(", ", folds)}]");
        Console.WriteLine($"champ w={w} edge={minEdge} EV≥{minEv} {(useDc ? $"DC α={dcAlpha}" : $"EMA α={alpha}")}");
        Console.WriteLine(
            $"{"fold",-12} {"nL",5} {"clvL",7} {"pnlL",7} {"llL",7} {"brL",7} {"kL",8} | {"nP",5} {"clvP",7} {"pnlP",7} {"llP",7} {"brP",7} {"kP",8} {"dPnL",7} {"dCLV",7}");

        var foldWins = 0;
        var nTotL = 0;
        var nTotP = 0;
        double sumClvL = 0, sumClvP = 0, sumPnlL = 0, sumPnlP = 0, sumKellyL = 0, sumKellyP = 0;

        foreach (var fold in folds)
        {
            SoccerOverProbConfig.ForceMode = false;
            var (_, holdL) = await RunHoldAsync(dbPath, sport, fold, w, minEv, minEdge, alpha, useDc, dcAlpha, ct);
            var kellyL = await KellyOnHoldAsync(dbPath, sport, fold, w, minEv, minEdge, alpha, useDc, dcAlpha, ct);

            SoccerOverProbConfig.ForceMode = true;
            var (_, holdP) = await RunHoldAsync(dbPath, sport, fold, w, minEv, minEdge, alpha, useDc, dcAlpha, ct);
            var kellyP = await KellyOnHoldAsync(dbPath, sport, fold, w, minEv, minEdge, alpha, useDc, dcAlpha, ct);

            var dPnl = holdP.PaperPnlUnits - holdL.PaperPnlUnits;
            var dClv = holdP.MeanClv - holdL.MeanClv;
            if (dPnl > 0) foldWins++;

            nTotL += holdL.Bets;
            nTotP += holdP.Bets;
            sumClvL += holdL.MeanClv * holdL.Bets;
            sumClvP += holdP.MeanClv * holdP.Bets;
            sumPnlL += holdL.PaperPnlUnits;
            sumPnlP += holdP.PaperPnlUnits;
            sumKellyL += kellyL;
            sumKellyP += kellyP;

            Console.WriteLine(
                $"{fold,-12} {holdL.Bets,5} {holdL.MeanClv,7:F3} {holdL.PaperPnlUnits,7:F1} {holdL.LogLoss,7:F4} {holdL.Brier,7:F4} {kellyL,8:F1} | {holdP.Bets,5} {holdP.MeanClv,7:F3} {holdP.PaperPnlUnits,7:F1} {holdP.LogLoss,7:F4} {holdP.Brier,7:F4} {kellyP,8:F1} {dPnl,7:F1} {dClv,7:F3}");
        }

        SoccerOverProbConfig.ForceMode = null; // restore KEEP set

        var meanClvL = nTotL == 0 ? 0 : sumClvL / nTotL;
        var meanClvP = nTotP == 0 ? 0 : sumClvP / nTotP;
        var majority = foldWins > folds.Count / 2;
        var clvOk = meanClvP >= meanClvL - 1e-12;
        var nOk = nTotP >= 40;
        var pass = majority && clvOk && nOk;

        Console.WriteLine(
            $"SUMMARY {sport} folds={folds.Count} winFolds={foldWins}/{folds.Count} nP={nTotP} meanClvL={meanClvL:F3} meanClvP={meanClvP:F3} pnlL={sumPnlL:F1} pnlP={sumPnlP:F1} kellyL={sumKellyL:F1} kellyP={sumKellyP:F1}");
        Console.WriteLine(
            $"VERDICT {sport} TierA={(pass ? "PASS" : "FAIL")} (majorityPnLΔ={majority}, clv≥champ={clvOk}, n≥40={nOk}) N_trials=1\n");
    }

    static (string Holdout, double W, double Edge, double Ev, double Alpha, bool Dc, double DcAlpha) Champ(string sport)
        => sport switch
        {
            "SERIEA" => ("2024-2025", SerieAChampConfig.MarketWeight, SerieAChampConfig.MinAbsTotalEdge,
                SerieAChampConfig.MinEv, SerieAChampConfig.DcAlpha, true, SerieAChampConfig.DcAlpha),
            "MLS" => ("2025", MlsChampConfig.MarketWeight, MlsChampConfig.MinAbsTotalEdge,
                MlsChampConfig.MinEv, MlsChampConfig.EmaAlpha, false, 0.12),
            "LIGUE1" => ("2024-2025", Ligue1ChampConfig.MarketWeight, Ligue1ChampConfig.MinAbsTotalEdge,
                Ligue1ChampConfig.MinEv, Ligue1ChampConfig.EmaAlpha, false, 0.12),
            "LIGAMX" => ("2024-2025", LigaMxChampConfig.MarketWeight, LigaMxChampConfig.MinAbsTotalEdge,
                LigaMxChampConfig.MinEv, LigaMxChampConfig.EmaAlpha, false, 0.12),
            _ => throw new ArgumentException($"No champ for {sport}"),
        };

    static async Task<List<string>> PreHoldoutSeasonsAsync(
        string dbPath, string sport, string holdout, CancellationToken ct)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={dbPath}").Options;
        await using var db = new AppDbContext(options);
        var seasons = await db.Games.AsNoTracking()
            .Where(g => g.Sport == sport && g.HomeScore != null && g.Season != null)
            .Select(g => g.Season!)
            .Distinct()
            .ToListAsync(ct);
        // Need open+close TOTAL with movement — approximate by any TOTAL open on that season's games
        var withOdds = new List<string>();
        foreach (var s in seasons.Where(s => string.CompareOrdinal(s, holdout) < 0).OrderBy(s => s))
        {
            var gameIds = await db.Games.AsNoTracking()
                .Where(g => g.Sport == sport && g.Season == s)
                .Select(g => g.Id)
                .ToListAsync(ct);
            var n = await db.OddsSnapshots.AsNoTracking()
                .CountAsync(o => o.Market == "TOTAL" && gameIds.Contains(o.GameId), ct);
            if (n >= 20) withOdds.Add(s);
        }
        return withOdds;
    }

    static Task<(Gate1aResult Train, Gate1aResult Hold)> RunHoldAsync(
        string dbPath, string sport, string fold, double w, double minEv, double minEdge,
        double alpha, bool useDc, double dcAlpha, CancellationToken ct)
    {
        if (sport == "MLS")
            return MlsTotalsBacktest.RunAsync(dbPath, w, minEv, minEdge, usePlatt: false, alpha: alpha,
                useDixonColes: useDc, dcAlpha: dcAlpha, holdoutSeason: fold, ct: ct);
        return SoccerLeagueTotalsBacktest.RunAsync(dbPath, sport, fold, w, minEv, minEdge,
            usePlatt: false, alpha: alpha, useDixonColes: useDc, dcAlpha: dcAlpha, ct: ct);
    }

    /// <summary>¼ Kelly ≤2% bankroll units on fold tickets (seed 1000, AssumedJuice −110).</summary>
    static async Task<double> KellyOnHoldAsync(
        string dbPath, string sport, string fold, double w, double minEv, double minEdge,
        double alpha, bool useDc, double dcAlpha, CancellationToken ct)
    {
        // Re-run collecting only hold bets with stake PnL — reuse RunHold + stake via ticket probs.
        // shortcut: flat Acc PnL is units at −110; Kelly tracked in a second pass over same path.
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={dbPath}").Options;
        await using var db = new AppDbContext(options);

        List<Game> games;
        if (sport == "MLS")
        {
            // Fold seasons are calendar years; treat fold as holdout season string.
            games = (await db.Games.AsNoTracking()
                    .Where(g => g.Sport == "MLS" && g.HomeScore != null)
                    .ToListAsync(ct))
                .OrderBy(g => g.TipoffUtc).ThenBy(g => g.Id).ToList();
        }
        else
        {
            games = (await db.Games.AsNoTracking()
                    .Where(g => g.Sport == sport && g.HomeScore != null)
                    .ToListAsync(ct))
                .OrderBy(g => g.TipoffUtc).ThenBy(g => g.Id).ToList();
        }

        var odds = await db.OddsSnapshots.AsNoTracking().Where(o => o.Market == "TOTAL").ToListAsync(ct);
        var byOdds = odds.GroupBy(o => o.GameId).ToDictionary(g => g.Key, g => g.ToList());
        Dictionary<int, MlsGameXg> xgByGame = [];
        if (sport == "MLS")
        {
            try { xgByGame = await db.MlsGameXgs.AsNoTracking().ToDictionaryAsync(x => x.GameId, ct); }
            catch { /* */ }
        }

        var goalsBook = new ScoringBook(alpha: alpha, leagueInit: 2.7);
        var xgBook = sport == "MLS" ? new ScoringBook(alpha: alpha, leagueInit: 2.7) : null;
        var seenXg = new HashSet<string>(StringComparer.Ordinal);
        var dcBook = useDc ? new DixonColesBook(alpha: dcAlpha) : null;
        var bank = 1000.0;
        var juice = -110;

        foreach (var g in games)
        {
            var season = g.Season ?? "";
            if (sport == "MLS" && season == "2026")
            {
                UpdateMls(g, xgByGame, xgBook!, goalsBook, seenXg, dcBook);
                continue;
            }
            if (sport != "MLS" && string.CompareOrdinal(season, fold) > 0)
            {
                goalsBook.Update(g.HomeTeam, g.AwayTeam, g.HomeScore!.Value, g.AwayScore!.Value);
                dcBook?.Update(g.HomeTeam, g.AwayTeam, g.HomeScore!.Value, g.AwayScore!.Value);
                continue;
            }

            byOdds.TryGetValue(g.Id, out var snaps);
            snaps ??= [];
            var open = snaps.FirstOrDefault(s => s.PriceKind == PriceKind.Open);
            var close = snaps.FirstOrDefault(s => s.PriceKind == PriceKind.Close);
            if (open?.Line is not { } line || close?.Line is not { } closeLine
                || Math.Abs(line - closeLine) < 0.25)
            {
                if (sport == "MLS") UpdateMls(g, xgByGame, xgBook!, goalsBook, seenXg, dcBook);
                else
                {
                    goalsBook.Update(g.HomeTeam, g.AwayTeam, g.HomeScore!.Value, g.AwayScore!.Value);
                    dcBook?.Update(g.HomeTeam, g.AwayTeam, g.HomeScore!.Value, g.AwayScore!.Value);
                }
                continue;
            }

            double lh, la;
            if (sport == "MLS")
            {
                var useXg = seenXg.Contains(g.HomeTeam) && seenXg.Contains(g.AwayTeam);
                (lh, la) = useXg
                    ? xgBook!.ExpectedLambdas(g.HomeTeam, g.AwayTeam)
                    : goalsBook.ExpectedLambdas(g.HomeTeam, g.AwayTeam);
            }
            else
            {
                (lh, la) = dcBook?.ExpectedLambdas(g.HomeTeam, g.AwayTeam)
                    ?? goalsBook.ExpectedLambdas(g.HomeTeam, g.AwayTeam);
            }

            var modelExp = lh + la;
            var exp = w * line + (1 - w) * modelExp;
            var edge = exp - line;
            var ou = TotalsMath.OuResult(g.HomeScore!.Value, g.AwayScore!.Value, line);
            var pOver = SoccerOverProbConfig.OverProb(lh, la, exp, line, sport);
            var isFold = sport == "MLS" ? season == fold : season == fold;

            if (isFold && Math.Abs(edge) >= minEdge && ou != 0)
            {
                if (edge > 0)
                {
                    if (OddsMath.ExpectedValue(pOver, juice) >= minEv)
                    {
                        var stake = bank * Math.Min(0.02, OddsMath.FractionalKelly(pOver, juice, 0.25));
                        if (stake >= 0.50)
                            bank += stake * TotalsMath.OuPnL(juice, ou);
                    }
                }
                else
                {
                    var pUnder = 1 - pOver;
                    if (OddsMath.ExpectedValue(pUnder, juice) >= minEv)
                    {
                        var stake = bank * Math.Min(0.02, OddsMath.FractionalKelly(pUnder, juice, 0.25));
                        if (stake >= 0.50)
                            bank += stake * TotalsMath.OuPnL(juice, -ou);
                    }
                }
            }

            if (sport == "MLS") UpdateMls(g, xgByGame, xgBook!, goalsBook, seenXg, dcBook);
            else
            {
                goalsBook.Update(g.HomeTeam, g.AwayTeam, g.HomeScore!.Value, g.AwayScore!.Value);
                dcBook?.Update(g.HomeTeam, g.AwayTeam, g.HomeScore!.Value, g.AwayScore!.Value);
            }
        }

        return bank - 1000.0;
    }

    static void UpdateMls(
        Game g, Dictionary<int, MlsGameXg> xgByGame, ScoringBook xgBook, ScoringBook goalsBook,
        HashSet<string> seenXg, DixonColesBook? dc)
    {
        goalsBook.Update(g.HomeTeam, g.AwayTeam, g.HomeScore!.Value, g.AwayScore!.Value);
        dc?.Update(g.HomeTeam, g.AwayTeam, g.HomeScore!.Value, g.AwayScore!.Value);
        if (!xgByGame.TryGetValue(g.Id, out var x)) return;
        xgBook.Update(g.HomeTeam, g.AwayTeam, x.HomeXg, x.AwayXg);
        seenXg.Add(g.HomeTeam);
        seenXg.Add(g.AwayTeam);
    }
}
