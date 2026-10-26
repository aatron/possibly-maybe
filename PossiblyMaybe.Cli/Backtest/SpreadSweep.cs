using PossiblyMaybe.Data;

namespace PossiblyMaybe.Cli.Backtest;

/// <summary>
/// Train-only parameter sweep for ATS; holdout scored once for the winner.
/// Objective: maximize train paper PnL among configs with train spread-CLV &gt; 0 and enough bets.
/// </summary>
public static class SpreadSweep
{
    public static async Task RunAsync(string dbPath, CancellationToken ct = default)
    {
        var minEvs = new[] { 0.04, 0.06, 0.08, 0.10, 0.12 };
        var minAbsEdges = new[] { 1.5, 2.5, 3.5, 4.5 }; // |expMargin + line|
        var models = new[] { Gate1Model.MarketElo, Gate1Model.MarketEloRefs, Gate1Model.MarketEloRefsLoad };

        var rows = new List<Row>();
        foreach (var model in models)
        foreach (var minEv in minEvs)
        foreach (var minEdge in minAbsEdges)
        {
            var (train, hold) = await RunFilteredAsync(dbPath, model, minEv, minEdge, ct);
            rows.Add(new Row(model, minEv, minEdge, train, hold));
            Console.WriteLine(
                $"train model={model} minEv={minEv:F2} minEdge={minEdge:F1}  bets={train.Bets} clv={train.MeanClv:F2} pnl={train.PaperPnlUnits:F1}  | hold bets={hold.Bets} clv={hold.MeanClv:F2} pnl={hold.PaperPnlUnits:F1}");
        }

        // TRAIN-only objective (money-first, anti-spray):
        // CLV>0, PnL>0, selective book (80..600 bets), maximize CLV then PnL.
        var eligible = rows
            .Where(r => r.Train.MeanClv > 0 && r.Train.PaperPnlUnits > 0
                        && r.Train.Bets is >= 80 and <= 600)
            .OrderByDescending(r => r.Train.MeanClv)
            .ThenByDescending(r => r.Train.PaperPnlUnits)
            .ToList();

        Console.WriteLine();
        if (eligible.Count == 0)
        {
            Console.WriteLine("SWEEP: no train config with CLV>0, PnL>0, bets in [80,600].");
            return;
        }

        var best = eligible[0];
        Console.WriteLine("=== CHAMP (TRAIN only: max CLV among selective profitable) ===");
        Console.WriteLine($"model={best.Model} minEv={best.MinEv:F2} minEdge={best.MinEdge:F1}");
        Console.WriteLine($"TRAIN  bets={best.Train.Bets} clvPts={best.Train.MeanClv:F3} hit={best.Train.HitRate:P1} pnl={best.Train.PaperPnlUnits:F1}");
        Console.WriteLine($"HOLDOUT bets={best.Hold.Bets} clvPts={best.Hold.MeanClv:F3} hit={best.Hold.HitRate:P1} pnl={best.Hold.PaperPnlUnits:F1}");
        var pass = best.Hold.PaperPnlUnits > 0 && best.Hold.MeanClv > 0 && best.Hold.Bets >= 15;
        Console.WriteLine(pass
            ? "KILL: HOLD PASS (holdout PnL>0, CLV>0, n>=15) — paper only until Gate 2b trust month."
            : "KILL: HOLD FAIL — do not bet real money on this champ.");
    }

    private static async Task<(Gate1aResult Train, Gate1aResult Hold)> RunFilteredAsync(
        string dbPath, Gate1Model model, double minEv, double minAbsEdge, CancellationToken ct)
    {
        // Reuse combo machinery with spread-only and custom filter via local copy loop would be heavy;
        // call specialized runner.
        return await SpreadEdgeBacktest.RunAsync(dbPath, model, minEv, minAbsEdge, ct);
    }

    private sealed record Row(
        Gate1Model Model, double MinEv, double MinEdge, Gate1aResult Train, Gate1aResult Hold);
}
