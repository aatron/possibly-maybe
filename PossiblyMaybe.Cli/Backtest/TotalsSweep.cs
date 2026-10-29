namespace PossiblyMaybe.Cli.Backtest;

/// <summary>
/// Train-only O/U sweep. Money objective: max train PnL among CLV&gt;0 configs with
/// enough volume (500..5000 train bets). Holdout season 2025 scored once.
/// Compares Scoring / PaceEff / PaceEffEfg. ISLP cal (Platt/Ridge) only at champ gates.
/// LogLoss/Brier are secondary (printed; do not change kill rule).
/// </summary>
public static class TotalsSweep
{
    public static async Task RunAsync(string dbPath, CancellationToken ct = default)
    {
        foreach (var model in new[] { TotalsModelKind.Scoring, TotalsModelKind.PaceEff, TotalsModelKind.PaceEffEfg })
            await RunModelAsync(dbPath, model, ct);

        await CompareChampGatesAsync(dbPath, ct);
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
            var (train, hold) = await TotalsEdgeBacktest.RunAsync(dbPath, w, minEv, minEdge, model, ct: ct);
            rows.Add(new Row(w, minEv, minEdge, train, hold));
            Console.WriteLine(
                $"train w={w:F2} minEdge={minEdge:F1}  bets={train.Bets} clv={train.MeanClv:F2} pnl={train.PaperPnlUnits:F1} ll={train.LogLoss:F4} br={train.Brier:F4}  | hold bets={hold.Bets} clv={hold.MeanClv:F2} pnl={hold.PaperPnlUnits:F1} ll={hold.LogLoss:F4} br={hold.Brier:F4}");
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
        Console.WriteLine($"TRAIN  bets={best.Train.Bets} clvPts={best.Train.MeanClv:F3} hit={best.Train.HitRate:P1} pnl={best.Train.PaperPnlUnits:F1} ll={best.Train.LogLoss:F4} br={best.Train.Brier:F4}");
        Console.WriteLine($"HOLDOUT bets={best.Hold.Bets} clvPts={best.Hold.MeanClv:F3} hit={best.Hold.HitRate:P1} pnl={best.Hold.PaperPnlUnits:F1} ll={best.Hold.LogLoss:F4} br={best.Hold.Brier:F4}");
        var pass = best.Hold.PaperPnlUnits > 0 && best.Hold.MeanClv > 0 && best.Hold.Bets >= 50;
        Console.WriteLine(pass
            ? $"KILL: HOLD PASS — {model}; paper Gate 2b before real cash."
            : $"KILL: HOLD FAIL — do not bet real money on {model} champ.");

        Console.WriteLine("--- top train PnL candidates ---");
        foreach (var r in eligible.Take(3))
            Console.WriteLine(
                $"  w={r.W:F2} edge={r.MinEdge:F1} trainPnL={r.Train.PaperPnlUnits:F1} n={r.Train.Bets} | holdPnL={r.Hold.PaperPnlUnits:F1} n={r.Hold.Bets} holdBr={r.Hold.Brier:F4}");
    }

    /// <summary>Same-gate compare vs PaceEff champ (w=0.5 edge≥6 EV≥4%).</summary>
    public static async Task CompareChampGatesAsync(string dbPath, CancellationToken ct = default)
    {
        const double w = 0.5, minEv = 0.04, minEdge = 6.0;
        Console.WriteLine();
        Console.WriteLine("######## SAME-GATE vs PaceEff champ (w=0.5 edge=6.0 EV≥4%) ########");
        Console.WriteLine($"{"Model",-14} {"hold_n",8} {"CLV",8} {"PnL",8} {"Brier",8} {"LogLoss",8}");

        Gate1aResult? paceHold = null;
        foreach (var model in new[]
                 {
                     TotalsModelKind.PaceEff,
                     TotalsModelKind.PaceEffPlatt,
                     TotalsModelKind.PaceEffRidge,
                 })
        {
            var (_, hold) = await TotalsEdgeBacktest.RunAsync(dbPath, w, minEv, minEdge, model, ct: ct);
            if (model == TotalsModelKind.PaceEff)
                paceHold = hold;
            Console.WriteLine(
                $"{model,-14} {hold.Bets,8} {hold.MeanClv,8:F2} {hold.PaperPnlUnits,8:F1} {hold.Brier,8:F4} {hold.LogLoss,8:F4}");
        }

        if (paceHold is null) return;
        Console.WriteLine(
            "Promote rule: hold PnL>0 AND CLV>0 AND (PnL or CLV beats PaceEff). ChampConfig unchanged unless both money metrics clear.");
        Console.WriteLine(
            $"PaceEff baseline: n={paceHold.Bets} CLV={paceHold.MeanClv:F2} PnL={paceHold.PaperPnlUnits:F1}");
    }

    /// <summary>Same-gate holdout: PaceEff vs PaceEffAbsence (tip-time DNP PPG proxy).</summary>
    public static async Task CompareAbsenceAsync(
        string dbPath,
        double absenceScale = 0.5,
        CancellationToken ct = default)
    {
        const double w = 0.5, minEv = 0.04, minEdge = 6.0;
        Console.WriteLine();
        Console.WriteLine(
            $"######## SAME-GATE PaceEff vs PaceEffAbsence (w={w} edge={minEdge} EV≥{minEv:P0} absenceScale={absenceScale}) ########");
        Console.WriteLine($"{"Model",-16} {"hold_n",8} {"CLV",8} {"PnL",8} {"Brier",8} {"LogLoss",8}");

        var (_, paceHold) = await TotalsEdgeBacktest.RunAsync(
            dbPath, w, minEv, minEdge, TotalsModelKind.PaceEff, ct: ct);
        Console.WriteLine(
            $"{TotalsModelKind.PaceEff,-16} {paceHold.Bets,8} {paceHold.MeanClv,8:F2} {paceHold.PaperPnlUnits,8:F1} {paceHold.Brier,8:F4} {paceHold.LogLoss,8:F4}");

        var (_, absHold) = await TotalsEdgeBacktest.RunAsync(
            dbPath, w, minEv, minEdge, TotalsModelKind.PaceEffAbsence,
            absenceScale: absenceScale, ct: ct);
        Console.WriteLine(
            $"{TotalsModelKind.PaceEffAbsence,-16} {absHold.Bets,8} {absHold.MeanClv,8:F2} {absHold.PaperPnlUnits,8:F1} {absHold.Brier,8:F4} {absHold.LogLoss,8:F4}");

        var pnlBeat = absHold.PaperPnlUnits > paceHold.PaperPnlUnits;
        var clvBeat = absHold.MeanClv > paceHold.MeanClv;
        var promote = absHold.PaperPnlUnits > 0 && absHold.MeanClv > 0 && (pnlBeat || clvBeat);
        Console.WriteLine(
            $"Verdict: Absence {(promote ? "BEATS" : "does not beat")} PaceEff on promote rule " +
            $"(PnL {(pnlBeat ? ">" : "<=")} baseline, CLV {(clvBeat ? ">" : "<=")} baseline).");
    }

    private sealed record Row(
        double W, double MinEv, double MinEdge, Gate1aResult Train, Gate1aResult Hold);
}
