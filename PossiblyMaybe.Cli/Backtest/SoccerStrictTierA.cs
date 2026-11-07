using Microsoft.EntityFrameworkCore;
using PossiblyMaybe.Data;
using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Cli.Backtest;

/// <summary>
/// Tier A for B7 <c>soccer-strict</c> vs league V0 (champ gates) on the same walk-forward tickets.
/// Strict: |edge|≥0.5 and EV ∈ [4%, 20%]. Keep = mean fold CLV ≥ V0 and n≥40.
/// </summary>
public static class SoccerStrictTierA
{
    public const string VariantId = "soccer-strict";
    public const int NTrials = 1; // locked edge/EV band

    public static async Task<bool> RunAsync(
        string dbPath,
        TrialLedger? trials = null,
        string? sportFilter = null,
        CancellationToken ct = default)
    {
        trials ??= new TrialLedger();
        Console.WriteLine("######## Tier A: B7 soccer-strict vs V0 (same tickets) ########");
        Console.WriteLine(
            $"Strict: |edge|≥{CalibrationFamily.SoccerStrictEdge} EV∈[{CalibrationFamily.StrictEvMin:P0},{CalibrationFamily.StrictEvMax:P0}]");
        Console.WriteLine($"N_trials={NTrials}. Oregon exclusion on. No Tier C. No paper promotion.\n");

        var sports = sportFilter is null
            ? new[] { "MLS", "LIGUE1", "LIGAMX", "SERIEA" }
            : new[] { sportFilter.ToUpperInvariant() };

        double sumClvV0 = 0, sumClvSt = 0, sumPnlV0 = 0, sumPnlSt = 0;
        var nFolds = 0;
        var foldWins = 0;
        var totalV0 = 0;
        var totalSt = 0;
        string? firstFold = null;
        string? lastFold = null;

        foreach (var sport in sports)
        {
            var (holdout, w, champEdge, minEv, alpha, useDc, dcAlpha) = Champ(sport);
            var folds = await PreHoldoutSeasonsAsync(dbPath, sport, holdout, ct);
            if (folds.Count == 0)
            {
                Console.WriteLine($"######## {sport} ######## no pre-holdout seasons with odds — skip");
                continue;
            }

            Console.WriteLine(
                $"######## {sport} ######## holdout(burned)={holdout} folds=[{string.Join(", ", folds)}]");
            Console.WriteLine(
                $"V0 w={w} |edge|≥{champEdge} EV≥{minEv:P0} {(useDc ? $"DC α={dcAlpha}" : $"EMA α={alpha}")}");
            Console.WriteLine(
                $"{"fold",-12} {"nV0",5} {"clvV0",7} {"pnlV0",7} {"llV0",7} {"brV0",7} |" +
                $" {"nSt",5} {"clvSt",7} {"pnlSt",7} {"llSt",7} {"brSt",7} {"dCLV",7}");

            foreach (var fold in folds)
            {
                firstFold ??= $"{sport}:{fold}";
                lastFold = $"{sport}:{fold}";
                var (v0, st) = await RunFoldAsync(
                    dbPath, sport, fold, holdout, w, minEv, champEdge, alpha, useDc, dcAlpha, ct);

                nFolds++;
                sumClvV0 += v0.MeanClv;
                sumClvSt += st.MeanClv;
                sumPnlV0 += v0.PaperPnlUnits;
                sumPnlSt += st.PaperPnlUnits;
                totalV0 += v0.Bets;
                totalSt += st.Bets;
                if (st.PaperPnlUnits > v0.PaperPnlUnits) foldWins++;

                Console.WriteLine(
                    $"{fold,-12} {v0.Bets,5} {v0.MeanClv,7:F3} {v0.PaperPnlUnits,7:F1} {v0.LogLoss,7:F4} {v0.Brier,7:F4} |" +
                    $" {st.Bets,5} {st.MeanClv,7:F3} {st.PaperPnlUnits,7:F1} {st.LogLoss,7:F4} {st.Brier,7:F4} {st.MeanClv - v0.MeanClv,7:F3}");
            }
        }

        if (nFolds == 0)
        {
            Console.WriteLine("VERDICT soccer-strict FAIL → Shadow marked FAIL (no folds)");
            return false;
        }

        var meanV0 = sumClvV0 / nFolds;
        var meanSt = sumClvSt / nFolds;
        var pass = TierAShadowVerdict.Pass(meanSt, meanV0, totalSt);

        Console.WriteLine();
        Console.WriteLine($"Mean fold CLV  V0={meanV0:F3}  soccer-strict={meanSt:F3}  Δ={meanSt - meanV0:F3}");
        Console.WriteLine($"Sum flat PnL   V0={sumPnlV0:F1}  soccer-strict={sumPnlSt:F1}  Δ={sumPnlSt - sumPnlV0:F1}");
        Console.WriteLine($"Bets total     V0={totalV0}  soccer-strict={totalSt}");
        Console.WriteLine($"PnL>V0 folds:  {foldWins}/{nFolds}");

        TierAShadowVerdict.Emit(
            trials, VariantId, pass,
            paramsJson: "{\"family\":\"b7\",\"tier\":\"A\",\"edge\":0.5,\"ev\":[0.04,0.20]}",
            dateFrom: firstFold ?? "none",
            dateTo: lastFold ?? "none",
            gridSize: NTrials);
        Console.WriteLine(trials.FormatReportFooter());
        return pass;
    }

    public static async Task<(Gate1aResult V0, Gate1aResult Strict)> RunFoldAsync(
        string dbPath,
        string sport,
        string testSeason,
        string holdoutSeason,
        double marketWeight,
        double minEv,
        double champEdge,
        double alpha,
        bool useDixonColes,
        double dcAlpha,
        CancellationToken ct = default)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;
        await using var db = new AppDbContext(options);

        var games = (await db.Games.AsNoTracking()
                .Where(g => g.Sport == sport && g.HomeScore != null)
                .ToListAsync(ct))
            .OrderBy(g => g.TipoffUtc).ThenBy(g => g.Id)
            .ToList();
        var odds = await db.OddsSnapshots.AsNoTracking()
            .Where(o => o.Market == "TOTAL")
            .ToListAsync(ct);
        var byOdds = odds.GroupBy(o => o.GameId).ToDictionary(g => g.Key, g => g.ToList());

        Dictionary<int, MlsGameXg> xgByGame = [];
        if (sport == "MLS")
        {
            try { xgByGame = await db.MlsGameXgs.AsNoTracking().ToDictionaryAsync(x => x.GameId, ct); }
            catch { /* older DB */ }
        }

        var goalsBook = new ScoringBook(alpha: alpha, leagueInit: 2.7);
        var xgBook = sport == "MLS" ? new ScoringBook(alpha: alpha, leagueInit: 2.7) : null;
        var seenXg = new HashSet<string>(StringComparer.Ordinal);
        DixonColesBook? dcGoals = useDixonColes ? new DixonColesBook(alpha: dcAlpha) : null;
        DixonColesBook? dcXg = useDixonColes && sport == "MLS" ? new DixonColesBook(alpha: dcAlpha) : null;

        var v0Acc = new Acc($"v0_{sport}_{testSeason}");
        var stAcc = new Acc($"strict_{sport}_{testSeason}");

        foreach (var g in games)
        {
            var season = g.Season ?? "";
            // Skip burned holdout and later; update books only past fold hold when walking folds.
            if (string.CompareOrdinal(season, holdoutSeason) >= 0
                || (sport == "MLS" && season == "2026"))
            {
                UpdateBooks(g, xgByGame, goalsBook, xgBook, seenXg, dcGoals, dcXg);
                continue;
            }

            byOdds.TryGetValue(g.Id, out var snaps);
            snaps ??= [];
            var open = snaps.FirstOrDefault(s => s.PriceKind == PriceKind.Open);
            var close = snaps.FirstOrDefault(s => s.PriceKind == PriceKind.Close);
            if (open?.Line is not { } line || close?.Line is not { } closeLine
                || Math.Abs(line - closeLine) < 0.25)
            {
                UpdateBooks(g, xgByGame, goalsBook, xgBook, seenXg, dcGoals, dcXg);
                continue;
            }

            var useXg = sport == "MLS" && seenXg.Contains(g.HomeTeam) && seenXg.Contains(g.AwayTeam);
            double lh, la;
            if (dcGoals is not null)
            {
                (lh, la) = useXg && dcXg is not null
                    ? dcXg.ExpectedLambdas(g.HomeTeam, g.AwayTeam)
                    : dcGoals.ExpectedLambdas(g.HomeTeam, g.AwayTeam);
            }
            else
            {
                (lh, la) = useXg && xgBook is not null
                    ? xgBook.ExpectedLambdas(g.HomeTeam, g.AwayTeam)
                    : goalsBook.ExpectedLambdas(g.HomeTeam, g.AwayTeam);
            }

            var modelExp = lh + la;
            var exp = marketWeight * line + (1 - marketWeight) * modelExp;
            var edge = exp - line;
            var juice = open.AmericanOdds == 0 ? -110 : open.AmericanOdds;
            var ou = TotalsMath.OuResult(g.HomeScore!.Value, g.AwayScore!.Value, line);
            var clv = TotalsMath.OverClvPoints(line, closeLine);
            var pOver = SoccerOverProbConfig.OverProb(lh, la, exp, line, sport);

            if (season == testSeason && !OregonExclusion.IsExcluded(sport, g.HomeTeam, g.AwayTeam))
            {
                TryBet(v0Acc, edge, champEdge, minEv, maxEv: null, pOver, juice, ou, clv);
                TryBet(
                    stAcc, edge, CalibrationFamily.SoccerStrictEdge,
                    CalibrationFamily.StrictEvMin, CalibrationFamily.StrictEvMax,
                    pOver, juice, ou, clv);
                v0Acc.AddGame();
                stAcc.AddGame();
            }

            UpdateBooks(g, xgByGame, goalsBook, xgBook, seenXg, dcGoals, dcXg);
        }

        return (v0Acc.ToResult(), stAcc.ToResult());
    }

    static void TryBet(
        Acc acc,
        double edge,
        double minAbsEdge,
        double minEv,
        double? maxEv,
        double pOver,
        int juice,
        int ou,
        double clvOver)
    {
        if (Math.Abs(edge) < minAbsEdge) return;
        var betOver = edge > 0;
        var p = betOver ? pOver : 1.0 - pOver;
        var ev = OddsMath.ExpectedValue(p, juice);
        if (ev < minEv) return;
        if (maxEv is { } cap && ev > cap) return;

        if (betOver)
            acc.AddBet(clvOver, ou > 0, TotalsMath.OuPnL(juice, ou), p);
        else
            acc.AddBet(-clvOver, ou < 0, TotalsMath.OuPnL(juice, -ou), p);
    }

    static void UpdateBooks(
        Game g,
        Dictionary<int, MlsGameXg> xgByGame,
        ScoringBook goalsBook,
        ScoringBook? xgBook,
        HashSet<string> seenXg,
        DixonColesBook? dcGoals,
        DixonColesBook? dcXg)
    {
        goalsBook.Update(g.HomeTeam, g.AwayTeam, g.HomeScore!.Value, g.AwayScore!.Value);
        dcGoals?.Update(g.HomeTeam, g.AwayTeam, g.HomeScore!.Value, g.AwayScore!.Value);
        if (xgBook is null) return;
        if (!xgByGame.TryGetValue(g.Id, out var xg)) return;
        xgBook.Update(g.HomeTeam, g.AwayTeam, xg.HomeXg, xg.AwayXg);
        dcXg?.Update(g.HomeTeam, g.AwayTeam, xg.HomeXg, xg.AwayXg);
        seenXg.Add(g.HomeTeam);
        seenXg.Add(g.AwayTeam);
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

    sealed class Acc(string slice)
    {
        int _games, _bets, _hits;
        double _clv, _pnl, _ll, _br;

        public void AddGame() => _games++;

        public void AddBet(double clv, bool won, double pnl, double p)
        {
            _bets++;
            _clv += clv;
            _pnl += pnl;
            if (won) _hits++;
            var pClip = Math.Clamp(p, 1e-6, 1 - 1e-6);
            _ll += -(won ? Math.Log(pClip) : Math.Log(1 - pClip));
            var y = won ? 1.0 : 0.0;
            _br += (pClip - y) * (pClip - y);
        }

        public Gate1aResult ToResult() => new()
        {
            Slice = slice,
            GamesScored = _games,
            Bets = _bets,
            MeanClv = _bets == 0 ? 0 : _clv / _bets,
            ClvUnit = "total_pts",
            HitRate = _bets == 0 ? 0 : (double)_hits / _bets,
            LogLoss = _bets == 0 ? 0 : _ll / _bets,
            Brier = _bets == 0 ? 0 : _br / _bets,
            PaperPnlUnits = _pnl,
        };
    }
}
