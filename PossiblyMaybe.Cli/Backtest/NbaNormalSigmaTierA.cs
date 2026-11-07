using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Cli.Backtest;

/// <summary>
/// Tier A walk-forward for NBA Normal residual-σ OverProb (0 free params: expanding train RMSE).
/// Pre-declared folds; no Tier B sorting; no Tier C.
/// </summary>
public static class NbaNormalSigmaTierA
{
    /// <summary>Pre-declared test seasons (TOTAL odds present; before holdout 2025).</summary>
    public static readonly string[] FoldSeasons =
        ["2015", "2016", "2017", "2018", "2019", "2020", "2021", "2022"];

    public const int NTrials = 1; // locked expanding RMSE; no grid

    public static async Task RunAsync(string dbPath, CancellationToken ct = default)
    {
        const double w = ChampConfig.MarketWeight, minEv = ChampConfig.MinEv, edge = ChampConfig.MinAbsTotalEdge;

        Console.WriteLine("######## Tier A: NBA Normal residual-σ OverProb ########");
        Console.WriteLine($"Gates: w={w} |exp−line|≥{edge} EV≥{minEv:P0}  AssumedJuice −110 when open odds=0");
        Console.WriteLine($"σ = expanding RMSE(actual−blendedExp); fallback={ExpandingResidualSigma.FallbackSigma} until n≥{ExpandingResidualSigma.MinN}");
        Console.WriteLine($"N_trials={NTrials} (0 free params). Folds: {string.Join(",", FoldSeasons)}");
        Console.WriteLine($"Holdout {TotalsEdgeBacktest.HoldoutSeason} not used for selection.\n");

        Console.WriteLine(
            $"{"fold",6} {"nL",5} {"clvL",7} {"pnlL",7} {"kL",7} {"brL",7} |" +
            $" {"nN",5} {"clvN",7} {"pnlN",7} {"kN",7} {"brN",7} {"σ",6} {"dPnL",7}");

        var foldWins = 0;
        var nFolds = 0;
        double sumClvL = 0, sumClvN = 0, sumPnlL = 0, sumPnlN = 0, sumKL = 0, sumKN = 0;
        var totalBetsL = 0;
        var totalBetsN = 0;

        foreach (var season in FoldSeasons)
        {
            var (logR, _, _) = await TotalsEdgeBacktest.RunSeasonFoldAsync(
                dbPath, season, w, minEv, edge, TotalsModelKind.PaceEff, ct: ct);
            var (normR, endSig, sigN) = await TotalsEdgeBacktest.RunSeasonFoldAsync(
                dbPath, season, w, minEv, edge, TotalsModelKind.PaceEffNormal, ct: ct);

            nFolds++;
            sumClvL += logR.MeanClv;
            sumClvN += normR.MeanClv;
            sumPnlL += logR.PaperPnlUnits;
            sumPnlN += normR.PaperPnlUnits;
            sumKL += logR.KellyQuarterPnl;
            sumKN += normR.KellyQuarterPnl;
            totalBetsL += logR.Bets;
            totalBetsN += normR.Bets;
            var dPnl = normR.PaperPnlUnits - logR.PaperPnlUnits;
            if (dPnl > 0) foldWins++;

            Console.WriteLine(
                $"{season,6} {logR.Bets,5} {logR.MeanClv,7:F2} {logR.PaperPnlUnits,7:F1} {logR.KellyQuarterPnl,7:F3} {logR.Brier,7:F4} |" +
                $" {normR.Bets,5} {normR.MeanClv,7:F2} {normR.PaperPnlUnits,7:F1} {normR.KellyQuarterPnl,7:F3} {normR.Brier,7:F4} {endSig,6:F2} {dPnl,7:F1}");
            _ = sigN;
        }

        var meanClvL = sumClvL / nFolds;
        var meanClvN = sumClvN / nFolds;
        Console.WriteLine();
        Console.WriteLine($"Mean fold CLV  logistic={meanClvL:F3}  normal={meanClvN:F3}");
        Console.WriteLine($"Sum flat PnL   logistic={sumPnlL:F1}  normal={sumPnlN:F1}  Δ={sumPnlN - sumPnlL:F1}");
        Console.WriteLine($"Sum Kelly¼    logistic={sumKL:F3}  normal={sumKN:F3}  Δ={sumKN - sumKL:F3}");
        Console.WriteLine($"Bets total    logistic={totalBetsL}  normal={totalBetsN}  (need n≥40 overall)");
        Console.WriteLine($"PnL>champ folds: {foldWins}/{nFolds}");

        var passClv = meanClvN >= meanClvL;
        var passMaj = foldWins * 2 > nFolds;
        var passN = totalBetsN >= 40;
        var tierA = passClv && passMaj && passN;
        Console.WriteLine(
            tierA
                ? $"Tier A PASS (provisional NBA — no Tier C). meanCLV≥champ={passClv} majorityΔPnL={passMaj} n≥40={passN}"
                : $"Tier A FAIL. meanCLV≥champ={passClv} majorityΔPnL={passMaj} n≥40={passN}");

        // Tier B report-only (never sorts; not KEEP alone).
        Console.WriteLine($"\n######## Tier B report-only (holdout {TotalsEdgeBacktest.HoldoutSeason}; not for selection) ########");
        var (_, holdL) = await TotalsEdgeBacktest.RunAsync(
            dbPath, w, minEv, edge, TotalsModelKind.PaceEff, ct: ct);
        var (_, holdN) = await TotalsEdgeBacktest.RunAsync(
            dbPath, w, minEv, edge, TotalsModelKind.PaceEffNormal, ct: ct);
        Console.WriteLine(
            $"logistic  n={holdL.Bets} clv={holdL.MeanClv:F2} pnl={holdL.PaperPnlUnits:F1} kelly¼={holdL.KellyQuarterPnl:F3} br={holdL.Brier:F4} ll={holdL.LogLoss:F4}");
        Console.WriteLine(
            $"normal    n={holdN.Bets} clv={holdN.MeanClv:F2} pnl={holdN.PaperPnlUnits:F1} kelly¼={holdN.KellyQuarterPnl:F3} br={holdN.Brier:F4} ll={holdN.LogLoss:F4}");
        Console.WriteLine("No Tier C for NBA. Do not place bets from this agent.");
    }
}
