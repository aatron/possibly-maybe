using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Cli.Backtest;

/// <summary>
/// Library sparsity / time-decay soft-spot test:
/// A) TRAIN-only EMA α (half-life) sweep at locked champ gates; holdout once.
/// B) At most one B2B feature (schedule-derived); soft-threshold OLS drop if |β|≈0.
/// Optional deflated holdout note when train cells ≥25 (Bailey/Lopez DSR caution).
/// </summary>
public static class TimeDecaySweep
{
    // Half-lives in games → α (plus exact baseline alphas injected per league).
    private static readonly double[] HalfLives = [3, 5, 8, 10, 15, 20, 30, 50];

    private static double[] AlphasWithBaseline(double baseline)
    {
        var set = new SortedSet<double>(HalfLives.Select(EmaDecay.AlphaFromHalfLife));
        set.Add(baseline);
        return set.Reverse().ToArray(); // faster α first
    }

    public static async Task RunAsync(string dbPath, CancellationToken ct = default)
    {
        Console.WriteLine("######## LIBRARY: time-decay α + sparse B2B gate ########");
        Console.WriteLine("Train peek for α; holdout scored once per (league, book). ChampConfig untouched unless KEEP.");
        Console.WriteLine();

        var summary = new List<SummaryRow>();

        await NbaAsync(dbPath, summary, ct);
        await MlsAsync(dbPath, summary, ct);
        await SoccerAsync(dbPath, "LIGUE1", "2024-2025",
            Ligue1ChampConfig.MarketWeight, Ligue1ChampConfig.MinEv, Ligue1ChampConfig.MinAbsTotalEdge,
            baselineAlpha: 0.18, summary, ct);
        foreach (var (sport, hold) in new[]
                 {
                     ("EFL", "2024-2025"), ("LIGAMX", "2024-2025"),
                     ("LALIGA", "2024-2025"), ("SERIEA", "2024-2025"),
                 })
        {
            // FAIL leagues: use same paper gates as champs (w=0.4 edge=0.25) for α retest
            await SoccerAsync(dbPath, sport, hold, 0.40, 0.04, 0.25,
                baselineAlpha: 0.18, summary, ct);
        }

        Console.WriteLine();
        Console.WriteLine("======== SUMMARY (holdout) ========");
        Console.WriteLine(
            $"{"League",-10} {"Variant",-28} {"n",5} {"CLV",7} {"PnL",7} {"$",6} {"vsBase",8} {"Gate",8}");
        foreach (var r in summary)
        {
            Console.WriteLine(
                $"{r.League,-10} {r.Variant,-28} {r.N,5} {r.Clv,7:F2} {r.Pnl,7:F1} {r.Dollars,6:F0} {r.VsBase,8} {r.Gate,8}");
        }
    }

    private static async Task NbaAsync(string dbPath, List<SummaryRow> summary, CancellationToken ct)
    {
        const double w = ChampConfig.MarketWeight, minEv = ChampConfig.MinEv, edge = ChampConfig.MinAbsTotalEdge;
        const double baseAlpha = 0.15;

        foreach (var model in new[] { TotalsModelKind.PaceEff, TotalsModelKind.Scoring })
        {
            Console.WriteLine($"---- NBA {model} @ w={w} edge={edge} ----");
            var (_, baseHold) = await TotalsEdgeBacktest.RunAsync(
                dbPath, w, minEv, edge, model, alpha: baseAlpha, ct: ct);
            // PaceEff money-path baseline is documented champ; Scoring only upgrades if it beats PaceEff.
            var baselinePnL = model == TotalsModelKind.PaceEff ? baseHold.PaperPnlUnits : 16.3;

            var trainCells = 0;
            Gate1aResult? bestTrain = null;
            double bestAlpha = baseAlpha;
            foreach (var a in AlphasWithBaseline(baseAlpha))
            {
                trainCells++;
                var (tr, _) = await TotalsEdgeBacktest.RunAsync(
                    dbPath, w, minEv, edge, model, alpha: a, ct: ct);
                Console.WriteLine(
                    $"  TRAIN α={a:F3} (hl≈{EmaDecay.HalfLifeFromAlpha(a):F1}) n={tr.Bets} clv={tr.MeanClv:F2} pnl={tr.PaperPnlUnits:F1}");
                if (tr.MeanClv <= 0 || tr.PaperPnlUnits <= 0 || tr.Bets < 200) continue;
                if (bestTrain is null || tr.PaperPnlUnits > bestTrain.PaperPnlUnits)
                {
                    bestTrain = tr;
                    bestAlpha = a;
                }
            }

            Gate1aResult hold;
            if (Math.Abs(bestAlpha - baseAlpha) < 1e-9)
                hold = baseHold; // already scored; no second hold peek
            else
                (_, hold) = await TotalsEdgeBacktest.RunAsync(
                    dbPath, w, minEv, edge, model, alpha: bestAlpha, ct: ct);
            NoteDsr(trainCells, hold.PaperPnlUnits - baselinePnL);
            summary.Add(Make(
                "NBA", $"{model} α={bestAlpha:F3}", hold, baselinePnL,
                requireBeatBaseline: true));

            // B: sparse B2B at baseline α (PaceEff only — money path)
            if (model != TotalsModelKind.PaceEff) continue;
            var beta = await TotalsEdgeBacktest.FitRestB2bPtsAsync(dbPath, model, baseAlpha, softThreshPts: 0.5, ct);
            Console.WriteLine($"  B2B soft-threshold β={beta:F3} (0 ⇒ drop)");
            if (beta == 0)
            {
                summary.Add(new SummaryRow("NBA", "PaceEff+B2B", 0, 0, 0, 0, "n/a", "SKIP"));
                continue;
            }

            var (_, restHold) = await TotalsEdgeBacktest.RunAsync(
                dbPath, w, minEv, edge, model, alpha: baseAlpha, restB2bPts: beta, ct: ct);
            summary.Add(Make("NBA", $"PaceEff+B2B β={beta:F2}", restHold, baselinePnL, requireBeatBaseline: true));
        }
    }

    private static async Task MlsAsync(string dbPath, List<SummaryRow> summary, CancellationToken ct)
    {
        const double w = MlsChampConfig.MarketWeight, minEv = MlsChampConfig.MinEv, edge = MlsChampConfig.MinAbsTotalEdge;
        const double baseAlpha = 0.18;
        Console.WriteLine($"---- MLS @ w={w} edge={edge} ----");

        var (_, baseHold) = await MlsTotalsBacktest.RunAsync(dbPath, w, minEv, edge, alpha: baseAlpha, ct: ct);
        var baselinePnL = baseHold.PaperPnlUnits;

        var trainCells = 0;
        Gate1aResult? bestTrain = null;
        double bestAlpha = baseAlpha;
        foreach (var a in AlphasWithBaseline(baseAlpha))
        {
            trainCells++;
            var (tr, _) = await MlsTotalsBacktest.RunAsync(dbPath, w, minEv, edge, alpha: a, ct: ct);
            Console.WriteLine(
                $"  TRAIN α={a:F3} (hl≈{EmaDecay.HalfLifeFromAlpha(a):F1}) n={tr.Bets} clv={tr.MeanClv:F2} pnl={tr.PaperPnlUnits:F1}");
            if (tr.MeanClv <= 0 || tr.PaperPnlUnits <= 0 || tr.Bets < 80) continue;
            if (bestTrain is null || tr.PaperPnlUnits > bestTrain.PaperPnlUnits)
            {
                bestTrain = tr;
                bestAlpha = a;
            }
        }

        Gate1aResult hold;
        if (Math.Abs(bestAlpha - baseAlpha) < 1e-9)
            hold = baseHold;
        else
            (_, hold) = await MlsTotalsBacktest.RunAsync(dbPath, w, minEv, edge, alpha: bestAlpha, ct: ct);
        NoteDsr(trainCells, hold.PaperPnlUnits - baselinePnL);
        summary.Add(Make("MLS", $"xG/goals α={bestAlpha:F3}", hold, baselinePnL, requireBeatBaseline: true));

        var beta = await MlsTotalsBacktest.FitRestB2bGoalsAsync(dbPath, baseAlpha, softThresh: 0.05, ct);
        Console.WriteLine($"  B2B soft-threshold β={beta:F3} (0 ⇒ drop)");
        if (beta == 0)
        {
            summary.Add(new SummaryRow("MLS", "B2B", 0, 0, 0, 0, "n/a", "SKIP"));
            return;
        }

        var (_, restHold) = await MlsTotalsBacktest.RunAsync(
            dbPath, w, minEv, edge, alpha: baseAlpha, restB2bGoals: beta, ct: ct);
        summary.Add(Make("MLS", $"B2B β={beta:F2}", restHold, baselinePnL, requireBeatBaseline: true));
    }

    private static async Task SoccerAsync(
        string dbPath, string sport, string holdout,
        double w, double minEv, double edge, double baselineAlpha,
        List<SummaryRow> summary, CancellationToken ct)
    {
        Console.WriteLine($"---- {sport} @ w={w} edge={edge} hold={holdout} ----");
        var (_, baseHold) = await SoccerLeagueTotalsBacktest.RunAsync(
            dbPath, sport, holdout, w, minEv, edge, alpha: baselineAlpha, ct: ct);
        var baselinePnL = baseHold.PaperPnlUnits; // same-run baseline (ignore stale docs)

        var trainCells = 0;
        Gate1aResult? bestTrain = null;
        double bestAlpha = baselineAlpha;
        foreach (var a in AlphasWithBaseline(baselineAlpha))
        {
            trainCells++;
            var (tr, _) = await SoccerLeagueTotalsBacktest.RunAsync(
                dbPath, sport, holdout, w, minEv, edge, alpha: a, ct: ct);
            Console.WriteLine(
                $"  TRAIN α={a:F3} (hl≈{EmaDecay.HalfLifeFromAlpha(a):F1}) n={tr.Bets} clv={tr.MeanClv:F2} pnl={tr.PaperPnlUnits:F1}");
            if (tr.MeanClv <= 0 || tr.PaperPnlUnits <= 0 || tr.Bets < 40) continue;
            if (bestTrain is null || tr.PaperPnlUnits > bestTrain.PaperPnlUnits)
            {
                bestTrain = tr;
                bestAlpha = a;
            }
        }

        Gate1aResult hold;
        if (Math.Abs(bestAlpha - baselineAlpha) < 1e-9)
            hold = baseHold;
        else
            (_, hold) = await SoccerLeagueTotalsBacktest.RunAsync(
                dbPath, sport, holdout, w, minEv, edge, alpha: bestAlpha, ct: ct);
        NoteDsr(trainCells, hold.PaperPnlUnits - baselinePnL);
        summary.Add(Make(sport, $"goals α={bestAlpha:F3}", hold, baselinePnL, requireBeatBaseline: true));

        // B2B only for Ligue1 champ path (one feature max per league money path)
        if (sport != "LIGUE1") return;
        var beta = await SoccerLeagueTotalsBacktest.FitRestB2bGoalsAsync(
            dbPath, sport, holdout, baselineAlpha, softThresh: 0.05, ct);
        Console.WriteLine($"  B2B soft-threshold β={beta:F3} (0 ⇒ drop)");
        if (beta == 0)
        {
            summary.Add(new SummaryRow(sport, "B2B", 0, 0, 0, 0, "n/a", "SKIP"));
            return;
        }

        var (_, restHold) = await SoccerLeagueTotalsBacktest.RunAsync(
            dbPath, sport, holdout, w, minEv, edge, alpha: baselineAlpha, restB2bGoals: beta, ct: ct);
        summary.Add(Make(sport, $"B2B β={beta:F2}", restHold, baselinePnL, requireBeatBaseline: true));
    }

    private static void NoteDsr(int trainCells, double holdMargin)
    {
        if (trainCells < 25) return;
        // shortcut: crude Bailey/Lopez caution — require positive margin; full DSR not implemented
        Console.WriteLine(
            $"  DSR-note: trainCells={trainCells}≥25; hold PnL margin vs baseline={holdMargin:F1}u (require >0 to trust)");
    }

    private static SummaryRow Make(
        string league, string variant, Gate1aResult hold, double baselinePnL, bool requireBeatBaseline)
    {
        var vs = hold.PaperPnlUnits - baselinePnL;
        var killOk = hold.Bets >= 40 && hold.PaperPnlUnits > 0 && hold.MeanClv > 0;
        var beats = vs > 0.05; // clear PnL improvement over same-run baseline
        var gate = killOk && (!requireBeatBaseline || beats) ? "KEEP" : "REVERT";
        // Scoring / non-money paths still need killOk; requireBeatBaseline forces beat-champ when set.

        return new SummaryRow(
            league, variant, hold.Bets, hold.MeanClv, hold.PaperPnlUnits,
            hold.PaperPnlUnits * 10, // $ @ 1u=$10 paper
            $"{vs:+0.0;-0.0}", gate);
    }

    private sealed record SummaryRow(
        string League, string Variant, int N, double Clv, double Pnl, double Dollars, string VsBase, string Gate);
}
