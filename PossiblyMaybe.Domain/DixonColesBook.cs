namespace PossiblyMaybe.Domain;

/// <summary>
/// Independent Poisson attack–defense (Dixon–Coles skeleton).
/// λ_h = μ · att_h · def_a · γ, λ_a = μ · att_a · def_h; total = λ_h+λ_a.
/// EMA α ≈ dashee87 ξ time-weight (recent games dominate); walk-forward only.
/// ρ lives only on the P(over) path (<see cref="OverProbWithRho"/>); book still EMA, no batch MLE.
/// </summary>
public sealed class DixonColesBook
{
    private readonly Dictionary<string, (double Att, double Def)> _t = new(StringComparer.Ordinal);
    private readonly double _alpha;
    private double _mu;
    private double _homeAdv;

    /// <param name="alpha">EMA weight on newest match (0.05–0.25 ≈ ξ half-life weeks–months).</param>
    /// <param name="leagueAvgGoals">League mean goals per team (half of typical total).</param>
    /// <param name="homeAdv">Multiplicative home advantage on home λ.</param>
    public DixonColesBook(double alpha = 0.12, double leagueAvgGoals = 1.35, double homeAdv = 1.25)
    {
        _alpha = alpha;
        _mu = leagueAvgGoals;
        _homeAdv = homeAdv;
    }

    public (double Home, double Away) ExpectedLambdas(string home, string away)
    {
        var h = Get(home);
        var a = Get(away);
        var lh = Math.Max(0.05, _mu * h.Att * a.Def * _homeAdv);
        var la = Math.Max(0.05, _mu * a.Att * h.Def);
        return (lh, la);
    }

    public double ExpectedTotal(string home, string away)
    {
        var (lh, la) = ExpectedLambdas(home, away);
        return lh + la;
    }

    public void Update(string home, string away, int homeScore, int awayScore)
        => Update(home, away, (double)homeScore, awayScore);

    public void Update(string home, string away, double homeScore, double awayScore)
    {
        var h = Get(home);
        var a = Get(away);
        var (lh, la) = ExpectedLambdas(home, away);

        // Split residual √ between att & def so α=1 recovers observed λ (no double-count).
        var rh = Math.Pow(Math.Max(homeScore, 0.05) / lh, _alpha * 0.5);
        var ra = Math.Pow(Math.Max(awayScore, 0.05) / la, _alpha * 0.5);

        _t[home] = (ClampFactor(h.Att * rh), ClampFactor(h.Def * ra));
        _t[away] = (ClampFactor(a.Att * ra), ClampFactor(a.Def * rh));

        var obsTotal = homeScore + awayScore;
        var predTotal = lh + la;
        _mu = (1 - _alpha * 0.25) * _mu + (_alpha * 0.25) * (obsTotal / 2.0);
        if (predTotal > 0.1)
        {
            var homeShareObs = homeScore / Math.Max(0.05, obsTotal);
            var homeSharePred = lh / predTotal;
            _homeAdv = ClampFactor((1 - _alpha * 0.15) * _homeAdv
                + (_alpha * 0.15) * _homeAdv * (homeShareObs / Math.Max(0.05, homeSharePred)));
        }
    }

    /// <summary>
    /// P(H+A &gt; line) under Dixon–Coles τ_ρ · Pois(λ_h) · Pois(λ_a). ρ=0 ⇒ independent Poisson sum.
    /// </summary>
    public static double OverProbWithRho(double lambdaHome, double lambdaAway, double line, double rho)
    {
        var lh = Math.Max(1e-9, lambdaHome);
        var la = Math.Max(1e-9, lambdaAway);
        if (Math.Abs(rho) < 1e-15)
            return TotalsMath.PoissonTotalOverProb(lh, la, line);

        // Keep τ ≥ 0 on the four adjusted cells.
        var maxAbs = Math.Min(1.0, Math.Min(1.0 / (lh * la), Math.Min(1.0 / lh, 1.0 / la)));
        rho = Math.Clamp(rho, -maxAbs + 1e-12, maxAbs - 1e-12);

        var kMax = (int)Math.Floor(line);
        if (kMax < 0) return 1.0;

        double cdf = 0;
        for (var n = 0; n <= kMax; n++)
        {
            for (var x = 0; x <= n; x++)
                cdf += JointPmF(x, n - x, lh, la, rho);
            if (cdf >= 1.0 - 1e-15) return 0.0;
        }
        return Math.Clamp(1.0 - cdf, 0.0, 1.0);
    }

    private static double JointPmF(int x, int y, double lh, double la, double rho)
        => Tau(x, y, lh, la, rho) * PoissonPmF(x, lh) * PoissonPmF(y, la);

    private static double Tau(int x, int y, double lh, double la, double rho)
    {
        if (x == 0 && y == 0) return 1.0 - lh * la * rho;
        if (x == 0 && y == 1) return 1.0 + lh * rho;
        if (x == 1 && y == 0) return 1.0 + la * rho;
        if (x == 1 && y == 1) return 1.0 - rho;
        return 1.0;
    }

    private static double PoissonPmF(int k, double lambda)
    {
        // e^{-λ} λ^k / k!
        var p = Math.Exp(-lambda);
        for (var i = 1; i <= k; i++)
            p *= lambda / i;
        return p;
    }

    private static double ClampFactor(double x) => Math.Clamp(x, 0.25, 4.0);

    private (double Att, double Def) Get(string team)
        => _t.TryGetValue(team, out var v) ? v : (1.0, 1.0);
}
