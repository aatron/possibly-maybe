using System.Runtime.CompilerServices;

namespace PossiblyMaybe.Domain;

/// <summary>League allow-list filters (V-SA0).</summary>
public static class LeagueFilterFamily
{
    public const int Order = 20;

    private static readonly HashSet<string> NoSerieA = new(StringComparer.OrdinalIgnoreCase)
    {
        "NBA", "MLS", "LIGUE1", "LIGAMX",
    };

    public static Variant VSa0 { get; } = new(
        "V-SA0", "no-seriea",
        NbaOverProbKind.NormalSigma,
        true, VariantRegistry.DefaultEdgeCapEv, true,
        VariantRegistry.DefaultNbaLineMoveK, VariantRegistry.DefaultSoccerLineMoveK,
        false, false, NoSerieA);

#pragma warning disable CA2255 // family self-register so wave-B is one file
    [ModuleInitializer]
    internal static void Init() => VariantRegistry.Register(Order, VSa0);
#pragma warning restore CA2255
}
