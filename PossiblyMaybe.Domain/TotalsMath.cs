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
