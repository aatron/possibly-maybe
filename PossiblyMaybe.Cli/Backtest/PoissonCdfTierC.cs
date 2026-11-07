using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Cli.Backtest;

/// <summary>
/// One-shot Tier C for poisson-cdf-pover. Parent only. Look-once: soccer 2025-2026.
/// MLS excluded (Tier A FAIL). Never re-run after results are logged.
/// </summary>
public static class PoissonCdfTierC
{
    public static async Task RunAsync(string dbPath, CancellationToken ct = default)
    {
        Console.WriteLine("######## Tier C LOOK-ONCE: poisson-cdf-pover vs logistic ########");
        Console.WriteLine("Holdout=2025-2026. AssumedJuice −110. N_trials carried from Tier A (=1/league).");
        Console.WriteLine("Leagues: SERIEA, LIGUE1, LIGAMX (MLS excluded — Tier A FAIL).\n");

        foreach (var sport in new[] { "SERIEA", "LIGUE1", "LIGAMX" })
        {
            var (holdout, w, edge, ev, alpha, useDc, dcAlpha) = Champ(sport);
            SoccerOverProbConfig.ForceMode = false;
            var (_, holdL) = await SoccerLeagueTotalsBacktest.RunAsync(
                dbPath, sport, holdout, w, ev, edge, usePlatt: false, alpha: alpha,
                useDixonColes: useDc, dcAlpha: dcAlpha, ct: ct);

            SoccerOverProbConfig.ForceMode = true;
            var (_, holdP) = await SoccerLeagueTotalsBacktest.RunAsync(
                dbPath, sport, holdout, w, ev, edge, usePlatt: false, alpha: alpha,
                useDixonColes: useDc, dcAlpha: dcAlpha, ct: ct);

            var clvOk = holdP.MeanClv >= holdL.MeanClv - 1e-12;
            var pnlOk = holdP.PaperPnlUnits > 0;
            var keep = clvOk && pnlOk;
            Console.WriteLine(
                $"TIER_C {sport} nL={holdL.Bets} clvL={holdL.MeanClv:F3} pnlL={holdL.PaperPnlUnits:F1} | " +
                $"nP={holdP.Bets} clvP={holdP.MeanClv:F3} pnlP={holdP.PaperPnlUnits:F1} | " +
                $"clv≥champ={clvOk} pnlP>0={pnlOk} KEEP={keep}");
        }

        SoccerOverProbConfig.ForceMode = null;
    }

    static (string Holdout, double W, double Edge, double Ev, double Alpha, bool Dc, double DcAlpha) Champ(string sport)
        => sport switch
        {
            "SERIEA" => ("2025-2026", SerieAChampConfig.MarketWeight, SerieAChampConfig.MinAbsTotalEdge,
                SerieAChampConfig.MinEv, SerieAChampConfig.DcAlpha, true, SerieAChampConfig.DcAlpha),
            "LIGUE1" => ("2025-2026", Ligue1ChampConfig.MarketWeight, Ligue1ChampConfig.MinAbsTotalEdge,
                Ligue1ChampConfig.MinEv, Ligue1ChampConfig.EmaAlpha, false, 0.12),
            "LIGAMX" => ("2025-2026", LigaMxChampConfig.MarketWeight, LigaMxChampConfig.MinAbsTotalEdge,
                LigaMxChampConfig.MinEv, LigaMxChampConfig.EmaAlpha, false, 0.12),
            _ => throw new ArgumentException(sport),
        };
}
