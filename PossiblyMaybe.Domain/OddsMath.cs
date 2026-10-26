namespace PossiblyMaybe.Domain;

/// <summary>Pure betting math: American/decimal odds, no-vig, EV, fractional Kelly.</summary>
public static class OddsMath
{
    public static double AmericanToDecimal(int american)
    {
        if (american == 0)
            throw new ArgumentOutOfRangeException(nameof(american), "American odds cannot be zero.");
        return american > 0
            ? 1.0 + american / 100.0
            : 1.0 + 100.0 / -american;
    }

    public static int DecimalToAmerican(double decimalOdds)
    {
        if (decimalOdds <= 1.0)
            throw new ArgumentOutOfRangeException(nameof(decimalOdds), "Decimal odds must be > 1.");
        if (decimalOdds >= 2.0)
            return (int)Math.Round((decimalOdds - 1.0) * 100.0);
        return (int)Math.Round(-100.0 / (decimalOdds - 1.0));
    }

    public static double ImpliedProbabilityFromAmerican(int american)
        => 1.0 / AmericanToDecimal(american);

    public static double ImpliedProbabilityFromDecimal(double decimalOdds)
    {
        if (decimalOdds <= 1.0)
            throw new ArgumentOutOfRangeException(nameof(decimalOdds), "Decimal odds must be > 1.");
        return 1.0 / decimalOdds;
    }

    /// <summary>American odds implied by paying <paramref name="buyPrice"/> for a $1 binary (Kalshi YES/NO ask).</summary>
    public static int AmericanFromBuyPrice(double buyPrice)
    {
        if (buyPrice is <= 0 or >= 1)
            throw new ArgumentOutOfRangeException(nameof(buyPrice), "Buy price must be in (0,1).");
        return DecimalToAmerican(1.0 / buyPrice);
    }

    /// <summary>Two-way multiplicative no-vig: normalize raw implied probs to sum to 1.</summary>
    public static (double ProbA, double ProbB) NoVigTwoWay(int americanA, int americanB)
    {
        var rawA = ImpliedProbabilityFromAmerican(americanA);
        var rawB = ImpliedProbabilityFromAmerican(americanB);
        var sum = rawA + rawB;
        if (sum <= 0)
            throw new ArgumentException("Implied probabilities must be positive.");
        return (rawA / sum, rawB / sum);
    }

    /// <summary>EV per unit stake at <paramref name="american"/> given true <paramref name="winProbability"/>.</summary>
    public static double ExpectedValue(double winProbability, int american)
    {
        if (winProbability is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(winProbability));
        return winProbability * AmericanToDecimal(american) - 1.0;
    }

    /// <summary>Fractional Kelly stake as a bankroll fraction (clamped at 0 when edge ≤ 0).</summary>
    public static double FractionalKelly(double winProbability, int american, double fraction = 0.25)
    {
        if (winProbability is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(winProbability));
        if (fraction < 0)
            throw new ArgumentOutOfRangeException(nameof(fraction));

        var b = AmericanToDecimal(american) - 1.0;
        if (b <= 0)
            return 0;
        var q = 1.0 - winProbability;
        var full = (b * winProbability - q) / b;
        return Math.Max(0, full * fraction);
    }
}
