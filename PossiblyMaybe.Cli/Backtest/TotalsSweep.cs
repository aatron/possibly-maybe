namespace PossiblyMaybe.Cli.Backtest;

/// <summary>
/// Train-only O/U sweep. Money objective: max train PnL among CLV&gt;0 configs with
/// enough volume (500..5000 train bets). Holdout season 2025 scored once.
/// Compares Scoring (legacy) vs PaceEff (box pace×ORtg).
/// </summary>
public static class TotalsSweep
{
    public static async Task RunAsync(string dbPath, CancellationToken ct = default)
    {
        foreach (var model in new[] { TotalsModelKind.Scoring, TotalsModelKind.PaceEff })
            await RunModelAsync(dbPath, model, ct);
    }

    private static async Task RunModelAsync(string dbPath, TotalsModelKind model, CancellationToken ct)
    {
        Console.WriteLine();
        Console.WriteLine($"######## MODEL={model} ########");
        const double minEv = 0.04;
        var marketWeights = new[] { 0.4, 0.5, 0.6, 0.7 };
        var minEdges = new[] { 2.0, 2.5, 3.0, 3.5, 4.0, 5.0, 6.0 };

        var rows = new List<Row>();
        foreach (var w in marketWeights)
        foreach (var minEdge in minEdges)
        {
            var (train, hold) = await TotalsEdgeBacktest.RunAsync(dbPath, w, minEv, minEdge, model, ct);
            rows.Add(new Row(w, minEv, minEdge, train, hold));
            Console.WriteLine(
                $"train w={w:F2} minEdge={minEdge:F1}  bets={train.Bets} clv={train.MeanClv:F2} pnl={train.PaperPnlUnits:F1}  | hold bets={hold.Bets} clv={hold.MeanClv:F2} pnl={hold.PaperPnlUnits:F1}");
        }

        // Money-first: volume band + max train PnL (not max CLV — that picked tiny books).
        var eligible = rows
            .Where(r => r.Train.MeanClv > 0 && r.Train.PaperPnlUnits > 0
                        && r.Train.Bets is >= 500 and <= 5000)
            .OrderByDescending(r => r.Train.PaperPnlUnits)
            .ThenByDescending(r => r.Train.MeanClv)
            .ToList();

        Console.WriteLine();
        if (eligible.Count == 0)
        {
            Console.WriteLine($"TOTALS SWEEP ({model}): no train config in volume band with CLV>0 and PnL>0.");
            Console.WriteLine($"KILL: HOLD FAIL — {model} not a money path yet.");
            return;
        }

        var best = eligible[0];
        Console.WriteLine($"=== TOTALS CHAMP model={model} (TRAIN: max PnL in volume band) ===");
        Console.WriteLine($"w={best.W:F2} minEv={best.MinEv:F2} minEdge={best.MinEdge:F1}");
        Console.WriteLine($"TRAIN  bets={best.Train.Bets} clvPts={best.Train.MeanClv:F3} hit={best.Train.HitRate:P1} pnl={best.Train.PaperPnlUnits:F1}");
        Console.WriteLine($"HOLDOUT bets={best.Hold.Bets} clvPts={best.Hold.MeanClv:F3} hit={best.Hold.HitRate:P1} pnl={best.Hold.PaperPnlUnits:F1}");
        var pass = best.Hold.PaperPnlUnits > 0 && best.Hold.MeanClv > 0 && best.Hold.Bets >= 50;
        Console.WriteLine(pass
            ? $"KILL: HOLD PASS — {model}; paper Gate 2b before real cash."
            : $"KILL: HOLD FAIL — do not bet real money on {model} champ.");

        Console.WriteLine("--- top train PnL candidates ---");
        foreach (var r in eligible.Take(3))
            Console.WriteLine(
                $"  w={r.W:F2} edge={r.MinEdge:F1} trainPnL={r.Train.PaperPnlUnits:F1} n={r.Train.Bets} | holdPnL={r.Hold.PaperPnlUnits:F1} n={r.Hold.Bets}");
    }

    private sealed record Row(
        double W, double MinEv, double MinEdge, Gate1aResult Train, Gate1aResult Hold);
}
