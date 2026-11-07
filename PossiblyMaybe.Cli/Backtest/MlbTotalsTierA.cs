using PossiblyMaybe.Domain;
using PossiblyMaybe.Domain.SportPacks;

namespace PossiblyMaybe.Cli.Backtest;

/// <summary>
/// C4 MLB Tier A walk-forward (2011–2019) + look-once Tier C 2021.
/// Gates locked in <see cref="MlbChampConfig"/> / c4_proof.md before scoring 2021.
/// </summary>
public static class MlbTotalsTierA
{
    public const int NTrials = 1;

    public static async Task RunAsync(string dbPath, string? parkFactorsPath = null, CancellationToken ct = default)
    {
        var pack = MlbSportPack.Instance;
        var w = MlbChampConfig.MarketWeight;
        var minEv = MlbChampConfig.MinEv;
        var edge = MlbChampConfig.MinAbsTotalEdge;
        var folds = pack.TierAFolds;

        Console.WriteLine("######## Tier A: MLB SP-FIP × park (runs-EMA fallback) ########");
        Console.WriteLine($"Gates locked: w={w} |exp−line|≥{edge} EV≥{minEv:P0} α={MlbChampConfig.EmaAlpha}  AssumedJuice −110");
        Console.WriteLine($"N_trials={NTrials}. Folds: {string.Join(",", folds)}");
        Console.WriteLine($"Skip {pack.SkipSeason} (not a fold). Tier C look-once {pack.TierCHoldoutSeason} not used for selection.");
        Console.WriteLine($"SpringSubMarketsReady={MlbSportPack.SpringSubMarketsReady} F5Model={MlbSportPack.F5ModelReady} TeamTotalModel={MlbSportPack.TeamTotalsModelReady}\n");

        Console.WriteLine($"{"fold",6} {"n",5} {"clv",7} {"pnl",7} {"hit",7}");

        var foldWins = 0;
        var nFolds = 0;
        double sumClv = 0, sumPnl = 0;
        var totalBets = 0;

        foreach (var season in folds)
        {
            var r = await MlbTotalsBacktest.RunSeasonFoldAsync(
                dbPath, season, w, minEv, edge, parkFactorsPath, ct);
            nFolds++;
            sumClv += r.MeanClv;
            sumPnl += r.PaperPnlUnits;
            totalBets += r.Bets;
            if (r.PaperPnlUnits > 0) foldWins++;
            Console.WriteLine(
                $"{season,6} {r.Bets,5} {r.MeanClv,7:F2} {r.PaperPnlUnits,7:F1} {r.HitRate,7:P1}");
        }

        var meanClv = nFolds == 0 ? 0 : sumClv / nFolds;
        Console.WriteLine();
        Console.WriteLine($"Mean fold CLV  {meanClv:F3}");
        Console.WriteLine($"Sum flat PnL   {sumPnl:F1}");
        Console.WriteLine($"Bets total     {totalBets}  (need n≥40 overall)");
        Console.WriteLine($"PnL>0 folds:   {foldWins}/{nFolds}");

        var passClv = meanClv > 0;
        var passMaj = foldWins * 2 > nFolds;
        var passN = totalBets >= 40;
        var tierA = passClv && passMaj && passN;
        Console.WriteLine(
            tierA
                ? $"Tier A PASS. meanCLV>0={passClv} majorityPnL={passMaj} n≥40={passN}"
                : $"Tier A FAIL. meanCLV>0={passClv} majorityPnL={passMaj} n≥40={passN}");

        Console.WriteLine($"\n######## Tier C look-once ({pack.TierCHoldoutSeason}; not for selection) ########");
        var hold = await MlbTotalsBacktest.RunSeasonFoldAsync(
            dbPath, pack.TierCHoldoutSeason, w, minEv, edge, parkFactorsPath, ct);
        Console.WriteLine(
            $"{pack.TierCHoldoutSeason} n={hold.Bets} clv={hold.MeanClv:F2} pnl={hold.PaperPnlUnits:F1} hit={hold.HitRate:P1}");
        var tierC = hold.Bets >= 40 && hold.MeanClv > 0 && hold.PaperPnlUnits > 0;
        Console.WriteLine(
            tierC
                ? "Tier C PASS (look-once report)."
                : "Tier C FAIL (look-once report).");
        Console.WriteLine("Do not place bets from this agent.");
    }
}
