namespace PossiblyMaybe.Domain;

public static class SpreadMath
{
    /// <summary>Home covers if margin + homeLine &gt; 0; push at 0.</summary>
    public static int HomeAtsResult(int homeScore, int awayScore, double homeLine)
    {
        var m = homeScore - awayScore + homeLine;
        if (m > 1e-9) return 1;
        if (m < -1e-9) return -1;
        return 0;
    }

    /// <summary>CLV in points for a home-side bet: open − close (e.g. −3 vs −5 → +2).</summary>
    public static double HomeSpreadClvPoints(double betHomeLine, double closeHomeLine)
        => betHomeLine - closeHomeLine;

    /// <summary>Map win probability to expected home margin (rough, CPU-cheap).</summary>
    public static double ExpectedHomeMargin(double homeWinProb, double scale = 28.0)
    {
        var p = Math.Clamp(homeWinProb, 0.01, 0.99);
        // centered logit → margin
        var logit = Math.Log(p / (1 - p));
        return logit * (scale / 4.0);
    }

    /// <summary>P(home covers) from margin edge vs line, logistic.</summary>
    public static double HomeCoverProb(double expectedMargin, double homeLine, double sharpness = 0.35)
    {
        var edge = expectedMargin + homeLine; // positive ⇒ home more likely to cover
        return 1.0 / (1.0 + Math.Exp(-sharpness * edge));
    }

    public static double AtsPnL(int americanJuice, int atsResult)
    {
        if (atsResult == 0) return 0;
        return atsResult > 0 ? OddsMath.AmericanToDecimal(americanJuice) - 1.0 : -1.0;
    }
}
