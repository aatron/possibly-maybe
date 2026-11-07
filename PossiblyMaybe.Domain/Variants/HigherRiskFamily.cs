using System.Runtime.CompilerServices;

namespace PossiblyMaybe.Domain;

/// <summary>
/// Higher-risk models (round 4 B8): split-conformal abstain filter and local-level Kalman ratings.
/// Kept for promotion only if Tier A mean CLV ≥ V0; losers stay registered (see b8_proof.md).
/// </summary>
public static class HigherRiskFamily
{
    public const int Order = 80;

    /// <summary>Split-conformal miscoverage (Angelopoulos–Bates selective abstain).</summary>
    public const double ConformalAlpha = 0.10;

    /// <summary>Obs noise r=1; q locked so steady-state Kalman gain equals PaceEff α=0.15.</summary>
    public const double KalmanObsNoise = 1.0;

    public const double KalmanProcessNoise = 0.15 * 0.15 / (1.0 - 0.15); // α²/(1−α)

    public static Variant Conformal { get; } = new(
        "+conformal", "+conformal",
        NbaOverProbKind.NormalSigma,
        false, VariantRegistry.DefaultEdgeCapEv, false,
        VariantRegistry.DefaultNbaLineMoveK, VariantRegistry.DefaultSoccerLineMoveK,
        false, false, null,
        ConformalFilter: true);

    public static Variant Kalman { get; } = new(
        "kalman", "kalman",
        NbaOverProbKind.NormalSigma,
        false, VariantRegistry.DefaultEdgeCapEv, false,
        VariantRegistry.DefaultNbaLineMoveK, VariantRegistry.DefaultSoccerLineMoveK,
        false, false, null,
        UseKalmanRatings: true);

    /// <summary>Gaussian conformal half-width when empirical scores are not on the candidate.</summary>
    public static double? GaussianHalfWidth(double residualSigma)
    {
        if (residualSigma <= 1e-9) return null;
        // Φ^{-1}(1 − α/2) for α=0.10 ≈ 1.64485
        return 1.6448536269514722 * residualSigma;
    }

    public static bool LineExcludedByInterval(double expectedTotal, double line, double halfWidth)
        => Math.Abs(expectedTotal - line) > halfWidth;

#pragma warning disable CA2255 // family self-register so wave-B is one file
    [ModuleInitializer]
    internal static void Init() => VariantRegistry.Register(Order, Conformal, Kalman);
#pragma warning restore CA2255
}

/// <summary>
/// Expanding absolute residual scores → split-conformal half-width (quantile of |y−ŷ|).
/// </summary>
public sealed class ConformalAbsResidual
{
    private readonly List<double> _scores = [];

    public int Count => _scores.Count;

    public void Observe(double residual) => _scores.Add(Math.Abs(residual));

    /// <summary>
    /// Half-width q̂ such that [ŷ±q̂] has ≈1−α coverage under exchangeability.
    /// null until <see cref="ExpandingResidualSigma.MinN"/> scores.
    /// </summary>
    public double? HalfWidth(double alpha = HigherRiskFamily.ConformalAlpha)
    {
        var n = _scores.Count;
        if (n < ExpandingResidualSigma.MinN) return null;
        var sorted = _scores.OrderBy(x => x).ToArray();
        // Finite-sample split-conformal index: ceil((n+1)(1−α)) − 1
        var idx = (int)Math.Ceiling((n + 1) * (1.0 - alpha)) - 1;
        idx = Math.Clamp(idx, 0, n - 1);
        return sorted[idx];
    }
}

/// <summary>Scalar local-level KF; steady-state gain matches EMA α when P is at equilibrium.</summary>
public sealed class LocalLevelKalman
{
    private readonly double _q;
    private readonly double _r;
    private double _x;
    private double _p;

    public LocalLevelKalman(double init, double processNoise, double obsNoise)
    {
        _x = init;
        _q = processNoise;
        _r = obsNoise;
        // Diffuse start so early games adapt faster than fixed-α EMA; settles to gain≈α.
        _p = 10.0 * obsNoise;
    }

    public double Value => _x;

    public void Update(double observation)
    {
        var pPred = _p + _q;
        var k = pPred / (pPred + _r);
        _x += k * (observation - _x);
        _p = (1 - k) * pPred;
    }

    /// <summary>Steady-state Kalman gain for local-level with noises q, r.</summary>
    public static double SteadyStateGain(double q, double r)
    {
        // P_pred = (q + sqrt(q² + 4qr)) / 2; K = P_pred / (P_pred + r)
        var disc = Math.Sqrt(q * q + 4 * q * r);
        var pPred = (q + disc) / 2.0;
        return pPred / (pPred + r);
    }
}

/// <summary>
/// Pace × efficiency book with per-stat local-level Kalman instead of fixed-α EMA.
/// Same ExpectedTotal geometry as <see cref="PaceEfficiencyBook"/>.
/// </summary>
public sealed class PaceEfficiencyKalmanBook
{
    private readonly Dictionary<string, TeamState> _t = new(StringComparer.Ordinal);
    private readonly double _q;
    private readonly double _r;
    private readonly double _initPace;
    private readonly double _initOrtg;

    public PaceEfficiencyKalmanBook(
        double processNoise = HigherRiskFamily.KalmanProcessNoise,
        double obsNoise = HigherRiskFamily.KalmanObsNoise,
        double initPace = 100,
        double initOrtg = 110)
    {
        _q = processNoise;
        _r = obsNoise;
        _initPace = initPace;
        _initOrtg = initOrtg;
    }

    public double ExpectedTotal(string home, string away)
    {
        var h = Get(home);
        var a = Get(away);
        var pace = 0.5 * (h.Pace.Value + a.Pace.Value);
        var homePts = pace * (h.Ortg.Value + a.Drtg.Value) / 200.0;
        var awayPts = pace * (a.Ortg.Value + h.Drtg.Value) / 200.0;
        return homePts + awayPts;
    }

    public void Update(
        string home, string away,
        double fgaH, double orebH, double tovH, double ftaH, double ptsH, double fg3aH,
        double fgaA, double orebA, double tovA, double ftaA, double ptsA, double fg3aA)
    {
        var possH = Math.Max(1, PaceEfficiencyBook.Possessions(fgaH, orebH, tovH, ftaH));
        var possA = Math.Max(1, PaceEfficiencyBook.Possessions(fgaA, orebA, tovA, ftaA));
        var pace = 0.5 * (possH + possA);
        var ortgH = 100.0 * ptsH / possH;
        var ortgA = 100.0 * ptsA / possA;
        var drtgH = 100.0 * ptsA / possH;
        var drtgA = 100.0 * ptsH / possA;
        var threeH = fgaH > 0 ? fg3aH / fgaH : 0.35;
        var threeA = fgaA > 0 ? fg3aA / fgaA : 0.35;

        var h = Get(home);
        var a = Get(away);
        h.Pace.Update(pace);
        h.Ortg.Update(ortgH);
        h.Drtg.Update(drtgH);
        h.ThreeRate.Update(threeH);
        a.Pace.Update(pace);
        a.Ortg.Update(ortgA);
        a.Drtg.Update(drtgA);
        a.ThreeRate.Update(threeA);
    }

    private TeamState Get(string team)
    {
        if (_t.TryGetValue(team, out var s)) return s;
        s = new TeamState(_initPace, _initOrtg, _q, _r);
        _t[team] = s;
        return s;
    }

    private sealed class TeamState
    {
        public LocalLevelKalman Pace { get; }
        public LocalLevelKalman Ortg { get; }
        public LocalLevelKalman Drtg { get; }
        public LocalLevelKalman ThreeRate { get; }

        public TeamState(double initPace, double initOrtg, double q, double r)
        {
            Pace = new LocalLevelKalman(initPace, q, r);
            Ortg = new LocalLevelKalman(initOrtg, q, r);
            Drtg = new LocalLevelKalman(initOrtg, q, r);
            ThreeRate = new LocalLevelKalman(0.35, q, r);
        }
    }
}
