using PossiblyMaybe.Domain;
using PossiblyMaybe.Domain.SportPacks;

namespace PossiblyMaybe.Cli.Backtest;

/// <summary>
/// Tier A refresh for shadow-only soccer packs (La Liga, EFL): walk-forward folds from the pack,
/// scored with frozen shadow gates. FAIL as champ is expected; still run for wave radar history.
/// CLI: tier-a-shadow-soccer [SPORT|all] [db]
/// </summary>
public static class SoccerShadowTierA
{
    public static async Task RunAsync(string dbPath, string? sportFilter = null, CancellationToken ct = default)
    {
        Console.WriteLine("######## Tier A: shadow soccer (goals EMA, FAIL leagues) ########");
        Console.WriteLine("Folds = pack.TierAFolds as holdouts; gates frozen on *ShadowConfig. N_trials=1/league.\n");

        var sports = sportFilter is null
            ? new[] { "LALIGA", "EFL" }
            : new[] { sportFilter.ToUpperInvariant() };

        foreach (var sport in sports)
        {
            try { await RunLeagueAsync(dbPath, sport, ct); }
            catch (Exception ex) { Console.WriteLine($"SKIP {sport}: {ex.Message}"); }
        }
    }

    static async Task RunLeagueAsync(string dbPath, string sport, CancellationToken ct)
    {
        if (!SportPackRegistry.TryGet(sport, out var pack))
            throw new ArgumentException($"No sport pack for {sport}");

        Console.WriteLine($"######## {sport} Tier A ######## holdout(burned)={pack.TierCHoldoutSeason} model={pack.Model}");
        Console.WriteLine($"gates w={pack.MarketWeight} edge={pack.MinAbsTotalEdge} EV≥{pack.MinEv} α={pack.EmaAlpha}");
        Console.WriteLine($"{"fold",-12} {"n",5} {"clv",7} {"pnl",7} {"hit",7} {"ll",7}");

        var nTot = 0;
        double sumClv = 0, sumPnl = 0;
        var foldsOk = 0;

        foreach (var fold in pack.TierAFolds)
        {
            var (_, hold) = await SoccerLeagueTotalsBacktest.RunAsync(
                dbPath, sport, fold, pack.MarketWeight, pack.MinEv, pack.MinAbsTotalEdge,
                usePlatt: false, alpha: pack.EmaAlpha, ct: ct);

            Console.WriteLine(
                $"{fold,-12} {hold.Bets,5} {hold.MeanClv,7:F3} {hold.PaperPnlUnits,7:F1} {hold.HitRate,7:P1} {hold.LogLoss,7:F4}");

            nTot += hold.Bets;
            sumClv += hold.MeanClv * hold.Bets;
            sumPnl += hold.PaperPnlUnits;
            if (hold.PaperPnlUnits > 0 && hold.MeanClv > 0 && hold.Bets >= 40)
                foldsOk++;
        }

        var meanClv = nTot == 0 ? 0 : sumClv / nTot;
        // Burned Tier C holdout once (already FAIL historically) — print for proof continuity
        var (_, burned) = await SoccerLeagueTotalsBacktest.RunAsync(
            dbPath, sport, pack.TierCHoldoutSeason, pack.MarketWeight, pack.MinEv, pack.MinAbsTotalEdge,
            usePlatt: false, alpha: pack.EmaAlpha, ct: ct);
        Console.WriteLine(
            $"burned {pack.TierCHoldoutSeason,-5} {burned.Bets,5} {burned.MeanClv,7:F3} {burned.PaperPnlUnits,7:F1} {burned.HitRate,7:P1} {burned.LogLoss,7:F4}");

        var pass = foldsOk > pack.TierAFolds.Count / 2 && meanClv > 0 && nTot >= 40
                   && burned.PaperPnlUnits > 0 && burned.MeanClv > 0 && burned.Bets >= 40;
        Console.WriteLine(
            $"SUMMARY {sport} folds={pack.TierAFolds.Count} foldPass={foldsOk}/{pack.TierAFolds.Count} n={nTot} meanClv={meanClv:F3} pnl={sumPnl:F1} burnedClv={burned.MeanClv:F3} burnedPnl={burned.PaperPnlUnits:F1}");
        Console.WriteLine(
            $"VERDICT {sport} TierA={(pass ? "PASS" : "FAIL")} (shadow stays on regardless) N_trials=1\n");
    }
}
