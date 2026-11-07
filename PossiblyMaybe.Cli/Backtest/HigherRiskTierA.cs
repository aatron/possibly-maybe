using Microsoft.EntityFrameworkCore;
using PossiblyMaybe.Data;
using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Cli.Backtest;

/// <summary>
/// Tier A for B8: +conformal (empirical abs-residual) and kalman ratings vs V0 (PaceEffNormal).
/// Keep rule (pre-registered): mean fold CLV ≥ V0 and n≥40. PASS → Shadow only.
/// </summary>
public static class HigherRiskTierA
{
    public static readonly string[] FoldSeasons = NbaNormalSigmaTierA.FoldSeasons;
    public const int NTrials = 2; // conformal α locked; kalman q locked to α=0.15

    public static async Task<(bool ConformalPass, bool KalmanPass)> RunAsync(
        string dbPath,
        TrialLedger? trials = null,
        CancellationToken ct = default)
    {
        trials ??= new TrialLedger();
        const double w = ChampConfig.MarketWeight, minEv = ChampConfig.MinEv, edge = ChampConfig.MinAbsTotalEdge;

        Console.WriteLine("######## Tier A: B8 higher-risk (+conformal, kalman) vs V0 ########");
        Console.WriteLine($"Gates: w={w} |exp−line|≥{edge} EV≥{minEv:P0}  juice −110 when open=0");
        Console.WriteLine($"Conformal α={HigherRiskFamily.ConformalAlpha} empirical |residual| quantile; minN={ExpandingResidualSigma.MinN}");
        Console.WriteLine($"Kalman q={HigherRiskFamily.KalmanProcessNoise:F5} r={HigherRiskFamily.KalmanObsNoise} (steady gain≈0.15)");
        Console.WriteLine($"N_trials={NTrials}. Folds: {string.Join(",", FoldSeasons)}");
        Console.WriteLine($"Holdout {TotalsEdgeBacktest.HoldoutSeason} not used for selection.\n");

        Console.WriteLine(
            $"{"fold",6} {"nV0",5} {"clvV0",7} {"pnlV0",7} {"nCf",5} {"clvCf",7} {"pnlCf",7} {"nKal",5} {"clvKal",7} {"pnlKal",7}");

        double sumClvV0 = 0, sumClvCf = 0, sumClvKal = 0;
        double sumPnlV0 = 0, sumPnlCf = 0, sumPnlKal = 0;
        var nFolds = 0;
        var foldWinsCf = 0;
        var foldWinsKal = 0;
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
            sumPnlV0 += v0.PaperPnlUnits;
            sumPnlCf += cf.PaperPnlUnits;
            sumPnlKal += kal.PaperPnlUnits;
            totalV0 += v0.Bets;
            totalCf += cf.Bets;
            totalKal += kal.Bets;
            if (cf.PaperPnlUnits > v0.PaperPnlUnits) foldWinsCf++;
            if (kal.PaperPnlUnits > v0.PaperPnlUnits) foldWinsKal++;
            Console.WriteLine(
                $"{season,6} {v0.Bets,5} {v0.MeanClv,7:F2} {v0.PaperPnlUnits,7:F1} {cf.Bets,5} {cf.MeanClv,7:F2} {cf.PaperPnlUnits,7:F1} {kal.Bets,5} {kal.MeanClv,7:F2} {kal.PaperPnlUnits,7:F1}");
        }

        var meanV0 = sumClvV0 / nFolds;
        var meanCf = sumClvCf / nFolds;
        var meanKal = sumClvKal / nFolds;
        Console.WriteLine();
        Console.WriteLine($"Mean fold CLV  V0={meanV0:F3}  +conformal={meanCf:F3}  kalman={meanKal:F3}");
        Console.WriteLine($"Sum flat PnL   V0={sumPnlV0:F1}  +conformal={sumPnlCf:F1}  kalman={sumPnlKal:F1}");
        Console.WriteLine($"Bets total     V0={totalV0}  +conformal={totalCf}  kalman={totalKal}");
        Console.WriteLine($"PnL>V0 folds   +conformal={foldWinsCf}/{nFolds}  kalman={foldWinsKal}/{nFolds}");

        var keepCf = TierAShadowVerdict.Pass(meanCf, meanV0, totalCf);
        var keepKal = TierAShadowVerdict.Pass(meanKal, meanV0, totalKal);

        var from = FoldSeasons[0];
        var to = FoldSeasons[^1];
        TierAShadowVerdict.Emit(
            trials, "+conformal", keepCf,
            paramsJson: "{\"family\":\"b8\",\"tier\":\"A\",\"alpha\":0.1}",
            dateFrom: from, dateTo: to, gridSize: 1);
        TierAShadowVerdict.Emit(
            trials, "kalman", keepKal,
            paramsJson: "{\"family\":\"b8\",\"tier\":\"A\",\"q\":0.15}",
            dateFrom: from, dateTo: to, gridSize: 1);
        Console.WriteLine(trials.FormatReportFooter());
        Console.WriteLine("No Tier C. Do not place bets from this agent.");
        return (keepCf, keepKal);
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
