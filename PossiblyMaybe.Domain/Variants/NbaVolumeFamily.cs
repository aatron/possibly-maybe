using System.Runtime.CompilerServices;

namespace PossiblyMaybe.Domain;

/// <summary>
/// NBA many-small vs few-big: PaceEff (and PaceEffEfg arm) at w=0.6, |edge|≥3.5.
/// Champ V0 is w=0.5 / edge 6.0 — see PROGRESS PaceEff volume alt.
/// </summary>
public static class NbaVolumeFamily
{
    public const int Order = 125; // B3
    public const double MarketWeight = 0.6;
    public const double MinAbsEdge = 3.5;

    private static readonly HashSet<string> NbaOnly = new(StringComparer.OrdinalIgnoreCase) { "NBA" };

    public static Variant NbaVolume { get; } = new(
        "nba-volume", "nba-paceeff-w0.6-e3.5",
        NbaOverProbKind.NormalSigma,
        EdgeCap: false, EdgeCapEv: VariantRegistry.DefaultEdgeCapEv,
        LineMove: false, NbaLineMoveK: VariantRegistry.DefaultNbaLineMoveK,
        SoccerLineMoveK: VariantRegistry.DefaultSoccerLineMoveK,
        LeagueShutoff: false, UseRealJuice: false, Leagues: NbaOnly,
        MarketWeight: MarketWeight, MinAbsEdge: MinAbsEdge,
        NbaModel: NbaTotalsModel.PaceEff);

    public static Variant NbaVolumeEfg { get; } = new(
        "nba-volume-efg", "nba-paceeff-efg-w0.6-e3.5",
        NbaOverProbKind.NormalSigma,
        false, VariantRegistry.DefaultEdgeCapEv, false,
        VariantRegistry.DefaultNbaLineMoveK, VariantRegistry.DefaultSoccerLineMoveK,
        false, false, NbaOnly,
        MarketWeight: MarketWeight, MinAbsEdge: MinAbsEdge,
        NbaModel: NbaTotalsModel.PaceEffEfg);

#pragma warning disable CA2255 // family self-register so wave-B is one file
    [ModuleInitializer]
    internal static void Init() => VariantRegistry.Register(Order, NbaVolume, NbaVolumeEfg);
#pragma warning restore CA2255
}
