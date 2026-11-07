namespace PossiblyMaybe.Domain;

/// <summary>
/// Blindside skips: absurd model EV, or market already moved against our side.
/// Tier A 2026-11-07 (N_trials=4 caps × NBA k∈{1.5,2}): RecommendedEdgeCap=0.25
/// (MLS/Ligue1 removed-set majority-neg; NBA edge-cap alone does not pass — high-EV kept winning),
/// RecommendedNbaLineMoveK=2.0 (8/8 folds removed PnL|CLV&lt;0), soccer k=0.5 + strike 8¢.
/// Re-run <c>tier-a-guards</c> after data refreshes.
/// </summary>
public static class Guards
{
    /// <summary>Skip when model EV exceeds this fraction (25%).</summary>
    public const double RecommendedEdgeCap = 0.25;

    /// <summary>NBA totals: skip when open→current moved ≥ this many points against our side.</summary>
    public const double RecommendedNbaLineMoveK = 2.0;

    /// <summary>Soccer totals: skip when open→current moved ≥ this many goals against our side.</summary>
    public const double RecommendedSoccerLineMoveK = 0.5;

    /// <summary>Binary/strike buy-price shift (8¢) treated as move-against when juice worsens.</summary>
    public const double RecommendedStrikeMove = 0.08;

    /// <summary>True when <paramref name="ev"/> (fraction, e.g. 0.30) exceeds <paramref name="cap"/>.</summary>
    public static bool ExceedsEdgeCap(double ev, double cap) => ev > cap;

    /// <summary>
    /// Totals line moved against <paramref name="side"/> ("over"/"under") by ≥ <paramref name="kPoints"/>.
    /// Over: current dropped vs open; under: current rose. Missing open → false (cannot detect).
    /// </summary>
    public static bool LineMovedAgainst(string side, double? openLine, double currentLine, double kPoints)
    {
        if (openLine is null || kPoints <= 0)
            return false;
        var delta = currentLine - openLine.Value;
        if (side.Equals("over", StringComparison.OrdinalIgnoreCase))
            return -delta >= kPoints;
        if (side.Equals("under", StringComparison.OrdinalIgnoreCase))
            return delta >= kPoints;
        return false;
    }

    /// <summary>
    /// Juice/strike worsened by ≥ <paramref name="kBuy"/> in implied buy price (e.g. 0.08 = 8¢).
    /// Higher implied at current American = worse price for the bettor on that quote.
    /// </summary>
    public static bool StrikeMovedAgainst(int? openAmerican, int currentAmerican, double kBuy = RecommendedStrikeMove)
    {
        if (openAmerican is null or 0 || currentAmerican == 0 || kBuy <= 0)
            return false;
        var openImp = OddsMath.ImpliedProbabilityFromAmerican(openAmerican.Value);
        var curImp = OddsMath.ImpliedProbabilityFromAmerican(currentAmerican);
        return curImp - openImp >= kBuy;
    }

    /// <summary>Soccer: line moved ≥ k goals against, or strike juice moved ≥ 8¢ against.</summary>
    public static bool SoccerMovedAgainst(
        string side,
        double? openLine,
        double currentLine,
        double kGoals,
        int? openAmerican,
        int currentAmerican,
        double kStrike = RecommendedStrikeMove)
        => LineMovedAgainst(side, openLine, currentLine, kGoals)
           || StrikeMovedAgainst(openAmerican, currentAmerican, kStrike);
}
