using PossiblyMaybe.Domain;
using PossiblyMaybe.Domain.SportPacks;

namespace PossiblyMaybe.Cli.Backtest;

/// <summary>
/// T6 / C3 redo: NFL EPA+roof Tier A folds with 2022 filled + real prices (no wind).
/// Wind arm stays on <see cref="NflWindTierA"/>. Shadow/radar only.
/// CLI: tier-a-nfl [db]
/// </summary>
public static class NflEpaTierA
{
    public const int NTrials = 1;

    public static async Task RunAsync(string dbPath, CancellationToken ct = default)
    {
        var pack = NflSportPack.Instance;
        var w = pack.MarketWeight;
        var minEv = pack.MinEv;
        var edge = pack.MinAbsTotalEdge;
        var holdout = pack.TierCHoldoutSeason;

        Console.WriteLine("######## Tier A: NFL EPA+roof (C3 redo / T6; no wind) ########");
        Console.WriteLine($"Gates (HOLD FAIL research): w={w} |exp−line|≥{edge} EV≥{minEv:P0}");
        Console.WriteLine("requireOpenClose=true; real AmericanOdds when present (else −110).");
        Console.WriteLine($"Holdout {holdout} not scored here (prior C3 burn). N_trials={NTrials}");
        Console.WriteLine($"Folds: {string.Join(",", pack.TierAFolds)}\n");
        Console.WriteLine($"{"fold",6} {"n",5} {"clv",7} {"pnl",7} {"hit",7}");

        var folds = pack.TierAFolds.Where(s => string.CompareOrdinal(s, holdout) < 0).OrderBy(s => s).ToList();
        var foldWins = 0;
        var nTot = 0;
        double sumClv = 0, sumPnl = 0;

        foreach (var season in folds)
        {
            var r = await NflTotalsBacktest.RunSeasonFoldAsync(
                dbPath, season, w, minEv, edge, applyWind: false, ct: ct);
            nTot += r.Bets;
            sumClv += r.MeanClv * r.Bets;
            sumPnl += r.PaperPnlUnits;
            if (r.PaperPnlUnits > 0) foldWins++;
            Console.WriteLine(
                $"{season,6} {r.Bets,5} {r.MeanClv,7:F2} {r.PaperPnlUnits,7:F1} {r.HitRate,7:P1}");
        }

        var meanClv = nTot == 0 ? 0 : sumClv / nTot;
        var majority = foldWins * 2 > folds.Count;
        var clvOk = meanClv > 0;
        var nOk = nTot >= 40;
        var pass = majority && clvOk && nOk;

        Console.WriteLine();
        Console.WriteLine(
            $"SUMMARY folds={folds.Count} winFolds={foldWins}/{folds.Count} n={nTot} meanClv={meanClv:F3} pnl={sumPnl:F1}");
        Console.WriteLine(
            $"VERDICT NFL EPA+roof TierA={(pass ? "PASS" : "FAIL")} (majorityPnL={majority}, meanCLV>0={clvOk}, n≥40={nOk}) N_trials={NTrials}");
        Console.WriteLine("Shadow/radar only — prior NFL HOLD FAIL still stands for money. Do not place bets.");
    }
}
