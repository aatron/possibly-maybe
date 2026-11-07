using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Cli.Backtest;

/// <summary>
/// Tier A: nba-volume / nba-volume-efg vs V0 champ gates. Pre-declared folds; holdout report-only.
/// </summary>
public static class NbaVolumeTierA
{
    public static readonly string[] FoldSeasons =
        ["2015", "2016", "2017", "2018", "2019", "2020", "2021", "2022"];

    public const int NTrials = 2; // volume PaceEff + volume PaceEffEfg (gates locked from r3)

    public static async Task RunAsync(string dbPath, CancellationToken ct = default)
    {
        const double w0 = ChampConfig.MarketWeight, e0 = ChampConfig.MinAbsTotalEdge, minEv = ChampConfig.MinEv;
        const double wV = NbaVolumeFamily.MarketWeight, eV = NbaVolumeFamily.MinAbsEdge;

        Console.WriteLine("######## Tier A: NBA volume (w=0.6 edge=3.5) vs V0 ########");
        Console.WriteLine($"V0 gates: PaceEff w={w0} |edge|≥{e0} EV≥{minEv:P0}");
        Console.WriteLine($"volume:   PaceEff / PaceEffEfg w={wV} |edge|≥{eV} EV≥{minEv:P0}");
        Console.WriteLine($"N_trials={NTrials}. Folds: {string.Join(",", FoldSeasons)}");
        Console.WriteLine($"Holdout {TotalsEdgeBacktest.HoldoutSeason} report-only (not for selection).\n");

        Console.WriteLine(
            $"{"fold",6} {"nV0",5} {"clvV0",7} {"pnlV0",7} |" +
            $" {"nVol",5} {"clvVol",7} {"pnlVol",7} {"dClv",7}");

        double sumClv0 = 0, sumClvV = 0, sumPnl0 = 0, sumPnlV = 0;
        var n0 = 0;
        var nV = 0;
        var nFolds = 0;

        foreach (var season in FoldSeasons)
        {
            var (r0, _, _) = await TotalsEdgeBacktest.RunSeasonFoldAsync(
                dbPath, season, w0, minEv, e0, TotalsModelKind.PaceEff, ct: ct);
            var (rV, _, _) = await TotalsEdgeBacktest.RunSeasonFoldAsync(
                dbPath, season, wV, minEv, eV, TotalsModelKind.PaceEff, ct: ct);

            nFolds++;
            sumClv0 += r0.MeanClv;
            sumClvV += rV.MeanClv;
            sumPnl0 += r0.PaperPnlUnits;
            sumPnlV += rV.PaperPnlUnits;
            n0 += r0.Bets;
            nV += rV.Bets;
            var dClv = rV.MeanClv - r0.MeanClv;
            Console.WriteLine(
                $"{season,6} {r0.Bets,5} {r0.MeanClv,7:F2} {r0.PaperPnlUnits,7:F1} |" +
                $" {rV.Bets,5} {rV.MeanClv,7:F2} {rV.PaperPnlUnits,7:F1} {dClv,7:F2}");
        }

        var mean0 = sumClv0 / nFolds;
        var meanV = sumClvV / nFolds;
        Console.WriteLine();
        Console.WriteLine($"Mean fold CLV/bet  V0={mean0:F3}  nba-volume={meanV:F3}  Δ={meanV - mean0:F3}");
        Console.WriteLine($"Sum flat PnL       V0={sumPnl0:F1}  nba-volume={sumPnlV:F1}  Δ={sumPnlV - sumPnl0:F1}");
        Console.WriteLine($"Bets total         V0={n0}  nba-volume={nV}");

        Console.WriteLine($"\n######## Holdout {TotalsEdgeBacktest.HoldoutSeason} (report-only) ########");
        var (_, h0) = await TotalsEdgeBacktest.RunAsync(
            dbPath, w0, minEv, e0, TotalsModelKind.PaceEff, ct: ct);
        var (_, hV) = await TotalsEdgeBacktest.RunAsync(
            dbPath, wV, minEv, eV, TotalsModelKind.PaceEff, ct: ct);
        var (_, hE) = await TotalsEdgeBacktest.RunAsync(
            dbPath, wV, minEv, eV, TotalsModelKind.PaceEffEfg, ct: ct);
        Console.WriteLine(
            $"V0               n={h0.Bets} clv/bet={h0.MeanClv:F2} pnl={h0.PaperPnlUnits:F1}");
        Console.WriteLine(
            $"nba-volume       n={hV.Bets} clv/bet={hV.MeanClv:F2} pnl={hV.PaperPnlUnits:F1}");
        Console.WriteLine(
            $"nba-volume-efg   n={hE.Bets} clv/bet={hE.MeanClv:F2} pnl={hE.PaperPnlUnits:F1}");
        Console.WriteLine("Rank by CLV/bet vs V0; PnL is secondary. No real-money promotion from this run.");
    }
}
