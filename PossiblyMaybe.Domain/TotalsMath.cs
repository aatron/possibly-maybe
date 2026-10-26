namespace PossiblyMaybe.Domain;

public static class TotalsMath
{
    /// <summary>1 = over, -1 = under, 0 = push.</summary>
    public static int OuResult(int homeScore, int awayScore, double totalLine)
    {
        var t = homeScore + awayScore - totalLine;
        if (t > 1e-9) return 1;
        if (t < -1e-9) return -1;
        return 0;
    }

    /// <summary>CLV pts for an over bet: close − open (higher close = better over number).</summary>
    public static double OverClvPoints(double betTotal, double closeTotal) => closeTotal - betTotal;

    public static double OverProb(double expectedTotal, double line, double sharpness = 0.25)
    {
        var edge = expectedTotal - line;
        return 1.0 / (1.0 + Math.Exp(-sharpness * edge));
    }

    public static double OuPnL(int americanJuice, int ouResult)
    {
        if (ouResult == 0) return 0;
        return ouResult > 0 ? OddsMath.AmericanToDecimal(americanJuice) - 1.0 : -1.0;
    }
}

/// <summary>Walk-forward team points-for / points-against EMA → expected game total.</summary>
public sealed class ScoringBook
{
    private readonly Dictionary<string, (double For, double Against)> _t = new(StringComparer.Ordinal);
    private readonly double _alpha;
    private readonly double _leagueInit;

    public ScoringBook(double alpha = 0.15, double leagueInit = 220)
    {
        _alpha = alpha;
        _leagueInit = leagueInit;
    }

    public double ExpectedTotal(string home, string away)
    {
        var h = Get(home);
        var a = Get(away);
        // classic: (home off vs away def) + (away off vs home def)
        return 0.5 * (h.For + a.Against) + 0.5 * (a.For + h.Against);
    }

    public void Update(string home, string away, int homeScore, int awayScore)
        => Update(home, away, (double)homeScore, awayScore);

    public void Update(string home, string away, double homeScore, double awayScore)
    {
        var h = Get(home);
        var a = Get(away);
        _t[home] = (
            (1 - _alpha) * h.For + _alpha * homeScore,
            (1 - _alpha) * h.Against + _alpha * awayScore);
        _t[away] = (
            (1 - _alpha) * a.For + _alpha * awayScore,
            (1 - _alpha) * a.Against + _alpha * homeScore);
    }

    private (double For, double Against) Get(string team)
        => _t.TryGetValue(team, out var v) ? v : (_leagueInit / 2, _leagueInit / 2);
}

/// <summary>
/// Industry-standard totals skeleton: pace × efficiency (ORtg/DRtg).
/// Possessions ≈ FGA − OREB + TOV + 0.44·FTA.
/// </summary>
public sealed class PaceEfficiencyBook
{
    private readonly Dictionary<string, (double Pace, double Ortg, double Drtg, double ThreeRate)> _t =
        new(StringComparer.Ordinal);
    private readonly double _alpha;
    private readonly double _initPace;
    private readonly double _initOrtg;

    public PaceEfficiencyBook(double alpha = 0.15, double initPace = 100, double initOrtg = 110)
    {
        _alpha = alpha;
        _initPace = initPace;
        _initOrtg = initOrtg;
    }

    public static double Possessions(double fga, double oreb, double tov, double fta)
        => fga - oreb + tov + 0.44 * fta;

    public double ExpectedTotal(string home, string away)
    {
        var h = Get(home);
        var a = Get(away);
        var pace = 0.5 * (h.Pace + a.Pace);
        var homePts = pace * (h.Ortg + a.Drtg) / 200.0;
        var awayPts = pace * (a.Ortg + h.Drtg) / 200.0;
        return homePts + awayPts;
    }

    /// <summary>Optional style lever: home 3PAr vs away (positive ⇒ more threes / higher variance scoring).</summary>
    public double ThreeRateEdge(string home, string away)
        => Get(home).ThreeRate - Get(away).ThreeRate;

    public void Update(
        string home, string away,
        double fgaH, double orebH, double tovH, double ftaH, double ptsH, double fg3aH,
        double fgaA, double orebA, double tovA, double ftaA, double ptsA, double fg3aA)
    {
        var possH = Math.Max(1, Possessions(fgaH, orebH, tovH, ftaH));
        var possA = Math.Max(1, Possessions(fgaA, orebA, tovA, ftaA));
        var pace = 0.5 * (possH + possA);
        var ortgH = 100.0 * ptsH / possH;
        var ortgA = 100.0 * ptsA / possA;
        var drtgH = 100.0 * ptsA / possH; // points allowed per own possession approx
        var drtgA = 100.0 * ptsH / possA;
        var threeH = fgaH > 0 ? fg3aH / fgaH : 0.35;
        var threeA = fgaA > 0 ? fg3aA / fgaA : 0.35;

        var h = Get(home);
        var a = Get(away);
        _t[home] = (
            (1 - _alpha) * h.Pace + _alpha * pace,
            (1 - _alpha) * h.Ortg + _alpha * ortgH,
            (1 - _alpha) * h.Drtg + _alpha * drtgH,
            (1 - _alpha) * h.ThreeRate + _alpha * threeH);
        _t[away] = (
            (1 - _alpha) * a.Pace + _alpha * pace,
            (1 - _alpha) * a.Ortg + _alpha * ortgA,
            (1 - _alpha) * a.Drtg + _alpha * drtgA,
            (1 - _alpha) * a.ThreeRate + _alpha * threeA);
    }

    private (double Pace, double Ortg, double Drtg, double ThreeRate) Get(string team)
        => _t.TryGetValue(team, out var v) ? v : (_initPace, _initOrtg, _initOrtg, 0.35);
}

/// <summary>Prior-season team eFG → points added to a pace×eff expected total.</summary>
public static class EfgPriorMath
{
    /// <summary>~1% eFG ≈ 2 pts on ~100 possessions → scale 200.</summary>
    public const double DefaultScale = 200.0;

    /// <summary>Season end-year 2025 (2024-25) → prior file key 2023_24.</summary>
    public static string PriorFileKey(int seasonEndYear)
        => $"{seasonEndYear - 2}_{(seasonEndYear - 1) % 100:D2}";

    public static double AdjPts(double efgHome, double efgAway, double leagueEfg, double scale = DefaultScale)
        => scale * ((efgHome + efgAway) / 2.0 - leagueEfg);
}
