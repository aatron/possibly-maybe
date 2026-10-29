namespace PossiblyMaybe.Domain;

/// <summary>
/// exp/poisson-cdf-pover: soccer P(over) from independent Poisson λs (default on this branch).
/// Set UsePoissonCdf=false to revert logistic sharpness 1.2. Tier A toggles for A/B.
/// </summary>
public static class SoccerOverProbConfig
{
    public static bool UsePoissonCdf = true;
    public const double LogisticSharpness = 1.2;

    public static double OverProb(double lambdaHome, double lambdaAway, double blendedExp, double line)
        => UsePoissonCdf
            ? TotalsMath.PoissonTotalOverProb(lambdaHome, lambdaAway, line)
            : TotalsMath.OverProb(blendedExp, line, sharpness: LogisticSharpness);
}
