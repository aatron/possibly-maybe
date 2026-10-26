namespace PossiblyMaybe.Domain;

public readonly record struct BetAdvice(
    bool Go,
    string Side,
    double ModelProb,
    double Ev,
    double StakeFraction,
    string Reason);

public static class BetDecider
{
    /// <summary>Manual-price advise: EV @ entered American odds, ¼ Kelly, min EV gate.</summary>
    public static BetAdvice Advise(
        double modelWinProb,
        int americanOdds,
        double bankroll,
        double minEv = 0.02,
        double kellyFraction = 0.25,
        double maxBetFrac = 0.02)
    {
        if (modelWinProb is < 0 or > 1)
            return new BetAdvice(false, "", modelWinProb, 0, 0, "invalid model probability");
        if (bankroll <= 0)
            return new BetAdvice(false, "", modelWinProb, 0, 0, "bankroll must be positive");

        var ev = OddsMath.ExpectedValue(modelWinProb, americanOdds);
        if (ev < minEv)
            return new BetAdvice(false, "side", modelWinProb, ev, 0, $"EV {ev:P2} below min {minEv:P0}");

        var frac = Math.Min(maxBetFrac, OddsMath.FractionalKelly(modelWinProb, americanOdds, kellyFraction));
        if (frac <= 0)
            return new BetAdvice(false, "side", modelWinProb, ev, 0, "no Kelly stake");

        return new BetAdvice(true, "side", modelWinProb, ev, frac, $"stake {frac * bankroll:F2} ({frac:P1} of bankroll)");
    }
}
