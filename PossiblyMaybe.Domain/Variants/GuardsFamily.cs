using System.Runtime.CompilerServices;

namespace PossiblyMaybe.Domain;

/// <summary>Guard stack on champ (V1–V5).</summary>
public static class GuardsFamily
{
    public const int Order = 10;

    public static Variant V1 { get; } = new(
        "V1", "+edgecap",
        NbaOverProbKind.NormalSigma,
        true, VariantRegistry.DefaultEdgeCapEv, false,
        VariantRegistry.DefaultNbaLineMoveK, VariantRegistry.DefaultSoccerLineMoveK,
        false, false, null);

    public static Variant V2 { get; } = new(
        "V2", "+linemove",
        NbaOverProbKind.NormalSigma,
        false, VariantRegistry.DefaultEdgeCapEv, true,
        VariantRegistry.DefaultNbaLineMoveK, VariantRegistry.DefaultSoccerLineMoveK,
        false, false, null);

    public static Variant V3 { get; } = new(
        "V3", "+edgecap+linemove",
        NbaOverProbKind.NormalSigma,
        true, VariantRegistry.DefaultEdgeCapEv, true,
        VariantRegistry.DefaultNbaLineMoveK, VariantRegistry.DefaultSoccerLineMoveK,
        false, false, null);

    // LeagueShutoff: pause sport when V3 rolling CLV (40) < 0; resume when ≥ 0.
    public static Variant V4 { get; } = new(
        "V4", "+shutoff",
        NbaOverProbKind.NormalSigma,
        true, VariantRegistry.DefaultEdgeCapEv, true,
        VariantRegistry.DefaultNbaLineMoveK, VariantRegistry.DefaultSoccerLineMoveK,
        true, false, null);

    public static Variant V5 { get; } = new(
        "V5", "+realjuice",
        NbaOverProbKind.NormalSigma,
        true, VariantRegistry.DefaultEdgeCapEv, true,
        VariantRegistry.DefaultNbaLineMoveK, VariantRegistry.DefaultSoccerLineMoveK,
        false, true, null);

#pragma warning disable CA2255 // family self-register so wave-B is one file
    [ModuleInitializer]
    internal static void Init() => VariantRegistry.Register(Order, V1, V2, V3, V4, V5);
#pragma warning restore CA2255
}
