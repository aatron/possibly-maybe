namespace PossiblyMaybe.Domain;

public static class Clv
{
    /// <summary>
    /// Closing-line value in probability points: no-vig model/bet price vs no-vig close,
    /// positive when the bet price implied a higher win chance than the close (got a better number).
    /// </summary>
    public static double ProbabilityClv(int betAmerican, int closeAmerican)
    {
        var betImp = OddsMath.ImpliedProbabilityFromAmerican(betAmerican);
        var closeImp = OddsMath.ImpliedProbabilityFromAmerican(closeAmerican);
        return betImp - closeImp;
    }
}
