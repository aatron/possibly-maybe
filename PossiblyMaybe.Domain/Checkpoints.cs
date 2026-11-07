namespace PossiblyMaybe.Domain;

/// <summary>
/// Sample-size timeline per variant × league (user 2026-11-07 / Round 4 R5).
/// Pass rules are pre-registered here; leader persistence uses <see cref="WaveRadar.PersistenceWeeks"/>.
/// </summary>
public enum CheckpointStage
{
    /// <summary>n &lt; 50 — too early to act.</summary>
    TooSoon = 0,
    /// <summary>~50 — bail only if CLV clearly &lt; 0.</summary>
    BailCheck = 1,
    /// <summary>100–200 (+ until 500) — CLV verdict keep / stop.</summary>
    ClvVerdict = 2,
    /// <summary>~500 — PnL loosely informative.</summary>
    PnlLoose = 3,
    /// <summary>1,500+ — PnL confirms; stake gate may allow &gt; ¼ Kelly / 2%.</summary>
    PnlConfirms = 4,
}

public sealed record CheckpointReport(
    int N,
    double MeanClv,
    double ClvSe,
    CheckpointStage Stage,
    string Verdict,
    bool AllowsWideStake);

/// <summary>Checkpoint reporter: n, CLV ± SE, stage, and pre-registered pass rules.</summary>
public static class Checkpoints
{
    public const int BailN = 50;
    public const int ClvVerdictN = 100;
    /// <summary>Upper end of the CLV-verdict band (informational; stage runs until <see cref="PnlLooseN"/>).</summary>
    public const int ClvVerdictEndN = 200;
    public const int PnlLooseN = 500;
    public const int PnlConfirmsN = 1500;
    /// <summary>z for "clearly negative" one-sided-ish CI (mean + z·SE &lt; 0).</summary>
    public const double ClearlyNegZ = 1.96;
    /// <summary>Leader must stay ahead this many weeks (same as A3 ladder).</summary>
    public const int LeaderPersistenceWeeks = WaveRadar.PersistenceWeeks;

    public static CheckpointStage StageFor(int n) => n switch
    {
        < BailN => CheckpointStage.TooSoon,
        < ClvVerdictN => CheckpointStage.BailCheck,
        < PnlLooseN => CheckpointStage.ClvVerdict,
        < PnlConfirmsN => CheckpointStage.PnlLoose,
        _ => CheckpointStage.PnlConfirms,
    };

    public static bool AllowsWideStake(int n, double meanClv)
        => n >= PnlConfirmsN && meanClv > 0;

    public static double StandardError(IReadOnlyList<double> clvPts)
    {
        var n = clvPts.Count;
        if (n < 2)
            return 0;
        var mean = clvPts.Average();
        double ss = 0;
        for (var i = 0; i < n; i++)
        {
            var d = clvPts[i] - mean;
            ss += d * d;
        }
        var sd = Math.Sqrt(ss / (n - 1));
        return sd / Math.Sqrt(n);
    }

    public static bool ClearlyNegative(double meanClv, double clvSe)
        => meanClv + ClearlyNegZ * clvSe < 0;

    public static string VerdictFor(CheckpointStage stage, double meanClv, double clvSe) => stage switch
    {
        CheckpointStage.TooSoon => "wait",
        CheckpointStage.BailCheck => ClearlyNegative(meanClv, clvSe) ? "bail" : "hold",
        CheckpointStage.ClvVerdict => meanClv > 0 ? "keep" : "stop",
        CheckpointStage.PnlLoose => "pnl-loose",
        CheckpointStage.PnlConfirms => meanClv > 0 ? "pnl-ok" : "pnl-fail",
        _ => "wait",
    };

    public static CheckpointReport Evaluate(IReadOnlyList<double> clvPts)
    {
        var n = clvPts.Count;
        var mean = n == 0 ? 0 : clvPts.Average();
        var se = StandardError(clvPts);
        var stage = StageFor(n);
        return new CheckpointReport(n, mean, se, stage, VerdictFor(stage, mean, se), AllowsWideStake(n, mean));
    }

    public static CheckpointReport Evaluate(int n, double meanClv, double clvSe)
    {
        var stage = StageFor(n);
        return new CheckpointReport(n, meanClv, clvSe, stage, VerdictFor(stage, meanClv, clvSe), AllowsWideStake(n, meanClv));
    }
}
