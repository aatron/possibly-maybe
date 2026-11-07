using System.Runtime.CompilerServices;

namespace PossiblyMaybe.Domain;

/// <summary>Champion / baseline configs (V0, V0L).</summary>
public static class ChampFamily
{
    public const int Order = 0;

    public static Variant V0 { get; } = new(
        VariantRegistry.V0Id, "champ-2026-11-07",
        NbaOverProbKind.NormalSigma,
        EdgeCap: false, EdgeCapEv: VariantRegistry.DefaultEdgeCapEv,
        LineMove: false, NbaLineMoveK: VariantRegistry.DefaultNbaLineMoveK,
        SoccerLineMoveK: VariantRegistry.DefaultSoccerLineMoveK,
        LeagueShutoff: false, UseRealJuice: false, Leagues: null);

    public static Variant V0L { get; } = new(
        "V0L", "legacy-logistic-nba",
        NbaOverProbKind.Logistic,
        false, VariantRegistry.DefaultEdgeCapEv, false,
        VariantRegistry.DefaultNbaLineMoveK, VariantRegistry.DefaultSoccerLineMoveK,
        false, false, null);

#pragma warning disable CA2255 // family self-register so wave-B is one file
    [ModuleInitializer]
    internal static void Init() => VariantRegistry.Register(Order, V0, V0L);
#pragma warning restore CA2255
}
