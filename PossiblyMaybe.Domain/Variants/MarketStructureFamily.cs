using System.Runtime.CompilerServices;

namespace PossiblyMaybe.Domain;

/// <summary>
/// Live market-structure filters (B6): Kaunitz consensus-outlier + line-move-follow.
/// Needs A2 tracker / live venue asks on the candidate when present; otherwise skips.
/// </summary>
public static class MarketStructureFamily
{
    public const int Order = 60;
    public const double DefaultAlpha = 0.05;

    /// <summary>
    /// Kaunitz: soft when max(Ω) &gt; 1/(p_cons − α), p_cons = 1/mean(Ω).
    /// Needs ≥2 venue decimals; otherwise false.
    /// </summary>
    public static bool IsSoftOutlier(IReadOnlyList<double> decimalOdds, double alpha = DefaultAlpha)
    {
        if (decimalOdds.Count < 2 || alpha <= 0 || alpha >= 1)
            return false;
        double sum = 0;
        double max = 0;
        foreach (var o in decimalOdds)
        {
            if (o <= 1.0) return false;
            sum += o;
            if (o > max) max = o;
        }
        var pCons = decimalOdds.Count / sum;
        if (pCons <= alpha)
            return false;
        return max > 1.0 / (pCons - alpha);
    }

    public static Variant ConsensusOutlier { get; } = new(
        "consensus-outlier", "kaunitz-venue-soft",
        NbaOverProbKind.NormalSigma,
        true, VariantRegistry.DefaultEdgeCapEv, true,
        VariantRegistry.DefaultNbaLineMoveK, VariantRegistry.DefaultSoccerLineMoveK,
        false, true, null,
        ConsensusOutlier: true, ConsensusAlpha: DefaultAlpha);

    public static Variant LineMoveFollow { get; } = new(
        "line-move-follow", "follow-open-to-current",
        NbaOverProbKind.NormalSigma,
        true, VariantRegistry.DefaultEdgeCapEv, false,
        VariantRegistry.DefaultNbaLineMoveK, VariantRegistry.DefaultSoccerLineMoveK,
        false, false, null,
        LineMoveFollow: true);

#pragma warning disable CA2255 // family self-register so wave-B is one file
    [ModuleInitializer]
    internal static void Init() => VariantRegistry.Register(Order, ConsensusOutlier, LineMoveFollow);
#pragma warning restore CA2255
}
