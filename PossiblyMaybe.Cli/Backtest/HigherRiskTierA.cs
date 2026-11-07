using Microsoft.EntityFrameworkCore;
using PossiblyMaybe.Data;
using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Cli.Backtest;

/// <summary>
/// Tier A for B8: +conformal (empirical abs-residual) and kalman ratings vs V0 (PaceEffNormal).
/// Keep rule (pre-registered): mean fold CLV ≥ V0; else register but document FAIL.
/// </summary>
public static class HigherRiskTierA
{
    public static readonly string[] FoldSeasons = NbaNormalSigmaTierA.FoldSeasons;
    public const int NTrials = 2; // conformal α locked; kalman q locked to α=0.15

    public static async Task RunAsync(string dbPath, CancellationToken ct = default)
    {
        const double w = ChampConfig.MarketWeight, minEv = ChampConfig.MinEv, edge = ChampConfig.MinAbsTotalEdge;

        Console.WriteLine("######## Tier A: B8 higher-risk (+conformal, kalman) vs V0 ########");
        Console.WriteLine($"Gates: w={w} |exp−line|≥{edge} EV≥{minEv:P0}  juice −110 when open=0");
        Console.WriteLine($"Conformal α={HigherRiskFamily.ConformalAlpha} empirical |residual| quantile; minN={ExpandingResidualSigma.MinN}");
        Console.WriteLine($"Kalman q={HigherRiskFamily.KalmanProcessNoise:F5} r={HigherRiskFamily.KalmanObsNoise} (steady gain≈0.15)");
        Console.WriteLine($"N_trials={NTrials}. Folds: {string.Join(",", FoldSeasons)}");
        Console.WriteLine($"Holdout {TotalsEdgeBacktest.HoldoutSeason} not used for selection.\n");

        Console.WriteLine(
            $"{"fold",6} {"nV0",5} {"clvV0",7} {"nCf",5} {"clvCf",7} {"nKal",5} {"clvKal",7}");

        double sumClvV0 = 0, sumClvCf = 0, sumClvKal = 0;
        var nFolds = 0;
        var totalV0 = 0;
        var totalCf = 0;
        var totalKal = 0;

        foreach (var season in FoldSeasons)
        {
            var (v0, cf, kal) = await RunFoldAsync(dbPath, season, w, minEv, edge, ct);
            nFolds++;
            sumClvV0 += v0.MeanClv;
            sumClvCf += cf.MeanClv;
            sumClvKal += kal.MeanClv;
            totalV0 += v0.Bets;
            totalCf += cf.Bets;
            totalKal += kal.Bets;
            Console.WriteLine(
                $"{season,6} {v0.Bets,5} {v0.MeanClv,7:F2} {cf.Bets,5} {cf.MeanClv,7:F2} {kal.Bets,5} {kal.MeanClv,7:F2}");
        }

        var meanV0 = sumClvV0 / nFolds;
        var meanCf = sumClvCf / nFolds;
        var meanKal = sumClvKal / nFolds;
        Console.WriteLine();
        Console.WriteLine($"Mean fold CLV  V0={meanV0:F3}  +conformal={meanCf:F3}  kalman={meanKal:F3}");
        Console.WriteLine($"Bets total     V0={totalV0}  +conformal={totalCf}  kalman={totalKal}");

        var keepCf = meanCf >= meanV0 && totalCf >= 40;
        var keepKal = meanKal >= meanV0 && totalKal >= 40;
        Console.WriteLine(
            keepCf
                ? $"+conformal KEEP (meanCLV≥V0={meanCf >= meanV0} n≥40={totalCf >= 40})"
                : $"+conformal FAIL keep (meanCLV≥V0={meanCf >= meanV0} n≥40={totalCf >= 40}) — still registered");
        Console.WriteLine(
            keepKal
                ? $"kalman KEEP (meanCLV≥V0={meanKal >= meanV0} n≥40={totalKal >= 40})"
                : $"kalman FAIL keep (meanCLV≥V0={meanKal >= meanV0} n≥40={totalKal >= 40}) — still registered");
        Console.WriteLine("No Tier C. Do not place bets from this agent.");
    }

    public static async Task<(Gate1aResult V0, Gate1aResult Conformal, Gate1aResult Kalman)> RunFoldAsync(
        string dbPath,
        string testSeason,
        double marketWeight,
        double minEv,
        double minAbsEdge,
        CancellationToken ct = default)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;
        await using var db = new AppDbContext(options);

        var games = (await db.Games.AsNoTracking()
                .Where(g => g.Sport == "NBA" && g.HomeScore != null && g.AwayScore != null)
                .ToListAsync(ct))
            .Where(g => string.CompareOrdinal(g.Season, TotalsEdgeBacktest.HoldoutSeason) < 0)
            .OrderBy(g => g.TipoffUtc).ThenBy(g => g.Id)
            .ToList();

        var odds = await db.OddsSnapshots.AsNoTracking()
            .Where(o => o.Market == "TOTAL")
            .ToListAsync(ct);
        var byGame = odds.GroupBy(o => o.GameId).ToDictionary(g => g.Key, g => g.ToList());

        Dictionary<int, GameBox> boxes = [];
        try { boxes = await db.GameBoxes.AsNoTracking().ToDictionaryAsync(b => b.GameId, ct); }
        catch { /* older DB */ }

        var pace = new PaceEfficiencyBook();
        var kalman = new PaceEfficiencyKalmanBook();
        var resid = new ExpandingResidualSigma();
        var conformal = new ConformalAbsResidual();
        var v0Acc = new Acc($"v0_{testSeason}");
        var cfAcc = new Acc($"cf_{testSeason}");
        var kalAcc = new Acc($"kal_{testSeason}");

        foreach (var g in games)
        {
            byGame.TryGetValue(g.Id, out var snaps);
            snaps ??= [];
            var open = snaps.FirstOrDefault(s => s.PriceKind == PriceKind.Open);
            var close = snaps.FirstOrDefault(s => s.PriceKind == PriceKind.Close);
            var emaExp = pace.ExpectedTotal(g.HomeTeam, g.AwayTeam);
            var kalExp = kalman.ExpectedTotal(g.HomeTeam, g.AwayTeam);

            // Drop garbage TOTAL opens (spreads / 10× typos) so CLV is comparable.
            if (open?.Line is { } line && close?.Line is { } closeLine
                && line is >= 170 and <= 280 && closeLine is >= 170 and <= 280
                && Math.Abs(line - closeLine) >= 0.25)
            {
                var juice = open.AmericanOdds == 0 ? -110 : open.AmericanOdds;
                var ou = TotalsMath.OuResult(g.HomeScore!.Value, g.AwayScore!.Value, line);
                var clvOver = TotalsMath.OverClvPoints(line, closeLine);
                var hw = conformal.HalfWidth();

                if (g.Season == testSeason)
                {
                    TryBet(v0Acc, emaExp, resid.Sigma, line, closeLine, juice, ou, clvOver,
                        marketWeight, minEv, minAbsEdge, g.HomeTeam, g.AwayTeam, conformalHw: null);
                    TryBet(cfAcc, emaExp, resid.Sigma, line, closeLine, juice, ou, clvOver,
                        marketWeight, minEv, minAbsEdge, g.HomeTeam, g.AwayTeam, conformalHw: hw);
                    TryBet(kalAcc, kalExp, resid.Sigma, line, closeLine, juice, ou, clvOver,
                        marketWeight, minEv, minAbsEdge, g.HomeTeam, g.AwayTeam, conformalHw: null);
                    v0Acc.AddGame();
                    cfAcc.AddGame();
                    kalAcc.AddGame();
                }

                var blended = marketWeight * line + (1 - marketWeight) * emaExp;
                resid.Observe(g.HomeScore!.Value + g.AwayScore!.Value - blended);
                conformal.Observe(g.HomeScore!.Value + g.AwayScore!.Value - blended);
            }

            if (boxes.TryGetValue(g.Id, out var box))
            {
                pace.Update(
                    g.HomeTeam, g.AwayTeam,
                    box.FgaHome, box.OrebHome, box.TovHome, box.FtaHome, box.PtsHome, box.Fg3aHome,
                    box.FgaAway, box.OrebAway, box.TovAway, box.FtaAway, box.PtsAway, box.Fg3aAway);
                kalman.Update(
                    g.HomeTeam, g.AwayTeam,
                    box.FgaHome, box.OrebHome, box.TovHome, box.FtaHome, box.PtsHome, box.Fg3aHome,
                    box.FgaAway, box.OrebAway, box.TovAway, box.FtaAway, box.PtsAway, box.Fg3aAway);
            }
        }

        return (v0Acc.ToResult(), cfAcc.ToResult(), kalAcc.ToResult());
    }

    private static void TryBet(
        Acc acc,
        double modelExp,
        double sigma,
        double line,
        double closeLine,
        int juice,
        int ou,
        double clvOver,
        double marketWeight,
        double minEv,
        double minAbsEdge,
        string home,
        string away,
        double? conformalHw)
    {
        if (OregonExclusion.IsExcluded("NBA", home, away)) return;
        var exp = marketWeight * line + (1 - marketWeight) * modelExp;
        var edge = exp - line;
        if (Math.Abs(edge) < minAbsEdge) return;

        var pOver = TotalsMath.NormalOverProb(exp, line, sigma);
        var betOver = edge > 0;
        var p = betOver ? pOver : 1.0 - pOver;
        var ev = OddsMath.ExpectedValue(p, juice);
        if (ev < minEv) return;

        if (conformalHw is { } hw && !HigherRiskFamily.LineExcludedByInterval(exp, line, hw))
            return;

        if (betOver)
            acc.AddBet(clvOver, ou > 0, TotalsMath.OuPnL(juice, ou));
        else
            acc.AddBet(-clvOver, ou < 0, TotalsMath.OuPnL(juice, -ou));
        _ = closeLine;
    }

    private sealed class Acc(string slice)
    {
        private int _games, _bets, _hits;
        private double _clv, _pnl;

        public void AddGame() => _games++;

        public void AddBet(double clv, bool won, double pnl)
        {
            _bets++;
            _clv += clv;
            _pnl += pnl;
            if (won) _hits++;
        }

        public Gate1aResult ToResult() => new()
        {
            Slice = slice,
            GamesScored = _games,
            Bets = _bets,
            MeanClv = _bets == 0 ? 0 : _clv / _bets,
            ClvUnit = "total_pts",
            HitRate = _bets == 0 ? 0 : (double)_hits / _bets,
            PaperPnlUnits = _pnl,
        };
    }
}
