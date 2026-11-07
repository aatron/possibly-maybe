using System.Runtime.CompilerServices;

namespace PossiblyMaybe.Domain;

/// <summary>NBA referee crew totals residual (<c>refs-nba</c>).</summary>
public static class RefsFamily
{
    public const int Order = 50;

    private static readonly HashSet<string> NbaOnly = new(StringComparer.OrdinalIgnoreCase)
    {
        "NBA",
    };

    /// <summary>
    /// Champ gates + crew foul/pace residual on <see cref="SlateCandidate.RefTotalsResidual"/>.
    /// Residual is filled by walk-forward <see cref="RefEffects"/> (Donaghy excluded from fit).
    /// </summary>
    public static Variant RefsNba { get; } = new(
        "refs-nba", "nba-ref-crew-totals",
        NbaOverProbKind.NormalSigma,
        false, VariantRegistry.DefaultEdgeCapEv, false,
        VariantRegistry.DefaultNbaLineMoveK, VariantRegistry.DefaultSoccerLineMoveK,
        false, false, NbaOnly, UseRefTotals: true);

#pragma warning disable CA2255 // family self-register so wave-B is one file
    [ModuleInitializer]
    internal static void Init() => VariantRegistry.Register(Order, RefsNba);
#pragma warning restore CA2255
}
