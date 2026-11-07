using PossiblyMaybe.Domain.SportPacks;

namespace PossiblyMaybe.Domain;

/// <summary>One simulation / nested-grid entry (R10). Grid size counts toward <see cref="TrialLedger.NTrials"/>.</summary>
public sealed record TrialEntry(
    string StrategyHash,
    string ParamsJson,
    string DateFrom,
    string DateTo,
    int GridSize,
    DateTimeOffset RecordedAt);

/// <summary>
/// Append-only trial log: every sim run records strategy hash + params + date range.
/// Reports print <see cref="NTrials"/> and deflated Sharpe / BH context automatically.
/// </summary>
public sealed class TrialLedger
{
    readonly List<TrialEntry> _entries = [];

    public IReadOnlyList<TrialEntry> Entries => _entries;

    /// <summary>Sum of grid sizes (a plain sim run uses GridSize=1).</summary>
    public int NTrials => _entries.Sum(e => e.GridSize);

    public TrialEntry Record(
        string strategyHash,
        string paramsJson,
        string dateFrom,
        string dateTo,
        int gridSize = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(strategyHash);
        if (gridSize < 1)
            throw new ArgumentOutOfRangeException(nameof(gridSize));

        var entry = new TrialEntry(
            strategyHash.Trim().ToLowerInvariant(),
            paramsJson ?? "",
            dateFrom,
            dateTo,
            gridSize,
            DateTimeOffset.UtcNow);
        _entries.Add(entry);
        return entry;
    }

    /// <summary>Footer line for every report: N_trials, DSR, BH multiplicity context.</summary>
    public string FormatReportFooter(double observedSharpe = 0, int nReturns = 0, double bhQ = WaveRadar.BhQ)
    {
        var n = NTrials;
        var dsr = DeflatedSharpe.Probability(observedSharpe, n, nReturns);
        return $"N_trials={n} DSR={dsr:F3} BH_q={bhQ:0.00} (adj over {n} trials)";
    }
}

/// <summary>
/// Bailey–López de Prado deflated Sharpe: P(SR̂ beats the expected max under N null trials).
/// shortcut: assumes non-skewed returns / independent trials; upgrade if skew·kurtosis matter.
/// </summary>
public static class DeflatedSharpe
{
    const double EulerMascheroni = 0.5772156649015329;

    /// <summary>E[max SR] under N independent null trials (unit variance SR scale).</summary>
    public static double ExpectedMaxNullSharpe(int nTrials)
    {
        if (nTrials <= 1)
            return 0;
        var n = (double)nTrials;
        // Φ^{-1} via inverse erf
        double Z(double p) => Math.Sqrt(2) * ErfInv(2 * p - 1);
        return (1 - EulerMascheroni) * Z(1 - 1 / n) + EulerMascheroni * Z(1 - 1 / (n * Math.E));
    }

    /// <summary>Standard error of the Sharpe ratio for T returns (Lo 2002, non-skewed).</summary>
    public static double SharpeStdError(double observedSharpe, int nReturns)
    {
        if (nReturns < 2)
            return 1;
        return Math.Sqrt((1 + 0.5 * observedSharpe * observedSharpe) / (nReturns - 1));
    }

    /// <summary>DSR ∈ (0,1): Φ((SR̂ − E[max]) / σ_SR).</summary>
    public static double Probability(double observedSharpe, int nTrials, int nReturns)
    {
        if (nTrials < 1)
            nTrials = 1;
        var se = SharpeStdError(observedSharpe, nReturns);
        if (se < 1e-12)
            return observedSharpe >= ExpectedMaxNullSharpe(nTrials) ? 1 : 0;
        var z = (observedSharpe - ExpectedMaxNullSharpe(nTrials)) / se;
        return NormalCdf(z);
    }

    static double NormalCdf(double z) => 0.5 * (1 + Erf(z / Math.Sqrt(2)));

    static double Erf(double x)
    {
        // Abramowitz–Stegun 7.1.26
        const double a1 = 0.254829592, a2 = -0.284496736, a3 = 1.421413741;
        const double a4 = -1.453152027, a5 = 1.061405429, p = 0.3275911;
        var sign = x < 0 ? -1 : 1;
        x = Math.Abs(x);
        var t = 1.0 / (1.0 + p * x);
        var y = 1.0 - (((((a5 * t + a4) * t) + a3) * t + a2) * t + a1) * t * Math.Exp(-x * x);
        return sign * y;
    }

    static double ErfInv(double x)
    {
        // Winitzki approximation
        if (x <= -1) return double.NegativeInfinity;
        if (x >= 1) return double.PositiveInfinity;
        const double a = 0.147;
        var ln = Math.Log(1 - x * x);
        var t1 = 2 / (Math.PI * a) + ln / 2;
        var t2 = ln / a;
        var sign = x < 0 ? -1 : 1;
        return sign * Math.Sqrt(Math.Sqrt(t1 * t1 - t2) - t1);
    }
}

/// <summary>
/// Nested walk-forward: tune only on train folds; grid size is charged to the trial ledger.
/// Vault / Skip seasons are never train folds.
/// </summary>
public static class NestedWalkForward
{
    public static IReadOnlyList<string> TrainFolds(SportPack pack)
    {
        ArgumentNullException.ThrowIfNull(pack);
        return pack.TierAFolds
            .Where(f =>
                !Vault.IsSealed(pack, f)
                && !f.Equals(pack.SkipSeason, StringComparison.OrdinalIgnoreCase)
                && !f.Equals("none", StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// Pick the grid cell with the best mean score across train folds.
    /// Records <paramref name="grid"/>.Count as trials (one charge for the whole search).
    /// </summary>
    public static TParam SelectOnTrain<TParam>(
        IReadOnlyList<string> trainFolds,
        IReadOnlyList<TParam> grid,
        Func<TParam, string, double> scoreFold,
        TrialLedger ledger,
        string strategyHash,
        string paramsLabel,
        string dateFrom,
        string dateTo)
    {
        ArgumentNullException.ThrowIfNull(trainFolds);
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(scoreFold);
        ArgumentNullException.ThrowIfNull(ledger);
        if (grid.Count == 0)
            throw new ArgumentException("Grid must be non-empty.", nameof(grid));
        if (trainFolds.Count == 0)
            throw new ArgumentException("Train folds must be non-empty.", nameof(trainFolds));

        ledger.Record(strategyHash, paramsLabel, dateFrom, dateTo, gridSize: grid.Count);

        TParam? best = default;
        var bestScore = double.NegativeInfinity;
        foreach (var p in grid)
        {
            var mean = trainFolds.Average(f => scoreFold(p, f));
            if (mean > bestScore)
            {
                bestScore = mean;
                best = p;
            }
        }

        return best!;
    }
}

/// <summary>
/// Promotion never from simulation alone: Tier A + vault unlocks live shadow (A3) only.
/// Paper / real require live ladder CLV — refused here if requested from a sim pass.
/// </summary>
public static class PromotionPolicy
{
    /// <summary>After sim Tier A + vault final: enter shadow. Never Paper/RealCandidate.</summary>
    public static LadderStage EnterLiveShadow(bool tierAPass, bool vaultFinalPass)
    {
        if (!tierAPass || !vaultFinalPass)
            throw new InvalidOperationException(
                "Simulation did not pass Tier A + vault; cannot enter the live shadow ladder.");
        return LadderStage.Shadow;
    }

    /// <summary>Refuse any request to promote to paper/real from simulation results alone.</summary>
    public static void RefuseSimOnlyPromotion(LadderStage requested)
    {
        if (requested is LadderStage.Paper or LadderStage.RealCandidate)
            throw new InvalidOperationException(
                "Promotion to paper/real never from simulation alone; " +
                "earn CLV on the live shadow ladder (A3).");
    }
}

/// <summary>R10 gate for <c>DayEngine</c>: vault on sims, trial logging, strategy hash.</summary>
public sealed class EngineRunOptions
{
    public EngineRunOptions(
        string strategyHash,
        bool isSimulation = false,
        bool final = false,
        VaultStore? vaultStore = null,
        TrialLedger? trials = null,
        string? paramsJson = null,
        string? rangeFrom = null,
        string? rangeTo = null)
    {
        StrategyHash = strategyHash;
        IsSimulation = isSimulation;
        Final = final;
        VaultStore = vaultStore;
        Trials = trials;
        ParamsJson = paramsJson ?? "";
        RangeFrom = rangeFrom;
        RangeTo = rangeTo;
    }

    public string StrategyHash { get; }
    public bool IsSimulation { get; }
    public bool Final { get; }
    public VaultStore? VaultStore { get; }
    public TrialLedger? Trials { get; }
    public string ParamsJson { get; }
    public string? RangeFrom { get; }
    public string? RangeTo { get; }

    public static EngineRunOptions Live(string strategyId, string? paramsJson = null)
        => new(Vault.StrategyHash(strategyId, paramsJson), isSimulation: false, paramsJson: paramsJson);

    public static EngineRunOptions Sim(
        string strategyHash,
        VaultStore vaultStore,
        TrialLedger trials,
        bool final = false,
        string? paramsJson = null,
        string? rangeFrom = null,
        string? rangeTo = null)
        => new(strategyHash, isSimulation: true, final, vaultStore, trials, paramsJson, rangeFrom, rangeTo);
}
