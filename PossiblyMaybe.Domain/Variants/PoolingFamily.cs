using System.Runtime.CompilerServices;

namespace PossiblyMaybe.Domain;

/// <summary>
/// Uncertainty / roster family (Round 4 B2).
/// <c>+pooling</c>: shrink model total toward league mean on early-season / promoted phases.
/// <c>+roster-reset</c>: same shrink + wider NBA σ after a major trade / transfer flag.
/// </summary>
public static class PoolingFamily
{
    public const int Order = 120;

    /// <summary>w in w·model + (1−w)·mean — early season ≈ k≈k0.</summary>
    public const double EarlySeasonWeight = 0.5;

    /// <summary>Heavier shrink after roster shock (less trust in team rates).</summary>
    public const double RosterResetWeight = 0.4;

    public const double RosterSigmaMul = 1.25;

    public static readonly HashSet<string> PoolingPhases = new(StringComparer.OrdinalIgnoreCase)
    {
        "opening-weeks",
        "early-season",
        "promoted",
        "promoted-first-games",
    };

    public static Variant VPool { get; } = new(
        "V-POOL", "+pooling",
        NbaOverProbKind.NormalSigma,
        EdgeCap: false, EdgeCapEv: VariantRegistry.DefaultEdgeCapEv,
        LineMove: false, NbaLineMoveK: VariantRegistry.DefaultNbaLineMoveK,
        SoccerLineMoveK: VariantRegistry.DefaultSoccerLineMoveK,
        LeagueShutoff: false, UseRealJuice: false, Leagues: null,
        Pooling: true, RosterReset: false);

    public static Variant VRoster { get; } = new(
        "V-ROSTER", "+roster-reset",
        NbaOverProbKind.NormalSigma,
        EdgeCap: false, EdgeCapEv: VariantRegistry.DefaultEdgeCapEv,
        LineMove: false, NbaLineMoveK: VariantRegistry.DefaultNbaLineMoveK,
        SoccerLineMoveK: VariantRegistry.DefaultSoccerLineMoveK,
        LeagueShutoff: false, UseRealJuice: false, Leagues: null,
        Pooling: false, RosterReset: true);

    /// <summary>Adjust candidate model inputs when this variant's flags apply; else identity.</summary>
    public static SlateCandidate Apply(Variant v, SlateCandidate c)
    {
        if (!v.Pooling && !v.RosterReset) return c;

        var exp = c.ModelExp;
        var sigma = c.NbaSigma;
        var lh = c.LambdaHome;
        var la = c.LambdaAway;
        var mean = LeagueMeanTotal(c.Sport);

        if (v.Pooling && c.SeasonPhase is { } phase && PoolingPhases.Contains(phase))
            (exp, lh, la) = ShrinkTriple(exp, lh, la, mean, EarlySeasonWeight);

        if (v.RosterReset && c.RosterFlag == true)
        {
            (exp, lh, la) = ShrinkTriple(exp, lh, la, mean, RosterResetWeight);
            sigma *= RosterSigmaMul;
        }

        return c with { ModelExp = exp, NbaSigma = sigma, LambdaHome = lh, LambdaAway = la };
    }

    public static double Shrink(double value, double mean, double wTowardModel)
        => wTowardModel * value + (1.0 - wTowardModel) * mean;

    // shortcut: fixed sport means until candidates store live league-mean totals
    public static double LeagueMeanTotal(string sport) =>
        sport.Equals("NBA", StringComparison.OrdinalIgnoreCase) ? 224.0 : 2.70;

    static (double Exp, double? Lh, double? La) ShrinkTriple(
        double exp, double? lh, double? la, double mean, double w)
    {
        exp = Shrink(exp, mean, w);
        if (lh is { } h) lh = Shrink(h, mean / 2.0, w);
        if (la is { } a) la = Shrink(a, mean / 2.0, w);
        return (exp, lh, la);
    }

#pragma warning disable CA2255 // family self-register so wave-B is one file
    [ModuleInitializer]
    internal static void Init() => VariantRegistry.Register(Order, VPool, VRoster);
#pragma warning restore CA2255
}
