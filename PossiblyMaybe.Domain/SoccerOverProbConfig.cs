namespace PossiblyMaybe.Domain;

/// <summary>
/// Soccer P(over): Poisson CDF for Tier-C KEEPs (SERIEA, LIGUE1); logistic elsewhere.
/// <see cref="ForceMode"/> lets Tier A A/B force all-on or all-off.
/// </summary>
public static class SoccerOverProbConfig
{
    public const double LogisticSharpness = 1.2;

    /// <summary>null = per-league KEEP set; true/false = force all leagues (Tier A only).</summary>
    public static bool? ForceMode;

    public static bool UsePoissonCdfFor(string sport)
        => ForceMode ?? sport is "SERIEA" or "LIGUE1";

    public static double OverProb(double lambdaHome, double lambdaAway, double blendedExp, double line, string sport)
        => UsePoissonCdfFor(sport)
            ? TotalsMath.PoissonTotalOverProb(lambdaHome, lambdaAway, line)
            : TotalsMath.OverProb(blendedExp, line, sharpness: LogisticSharpness);
}
