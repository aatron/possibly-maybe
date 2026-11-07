using System.Runtime.CompilerServices;

namespace PossiblyMaybe.Domain;

/// <summary>Blend-weight variants (inv-var fusion vs fixed champ w).</summary>
public static class BlendFamily
{
    public const int Order = 90;
    public const string InvVarId = "inv-var-blend";

    // shortcut: locked MoM σ from exp_inv-var-blend-w last folds (NBA 2022, MLS 2024); upgrade when candidates carry train residual moments
    public const double NbaSigmaModel = 18.55;
    public const double NbaSigmaLine = 23.26;
    public const double SoccerSigmaModel = 1.72;
    public const double SoccerSigmaLine = 1.73;

    public static Variant InvVar { get; } = new(
        InvVarId, "inv-var-blend",
        NbaOverProbKind.NormalSigma,
        false, VariantRegistry.DefaultEdgeCapEv, false,
        VariantRegistry.DefaultNbaLineMoveK, VariantRegistry.DefaultSoccerLineMoveK,
        false, false, null, InvVarBlend: true);

    public static (double SigmaModel, double SigmaLine) LockedSigmas(string sport)
        => sport.Equals("NBA", StringComparison.OrdinalIgnoreCase)
            ? (NbaSigmaModel, NbaSigmaLine)
            : (SoccerSigmaModel, SoccerSigmaLine);

#pragma warning disable CA2255 // family self-register so wave-B is one file
    [ModuleInitializer]
    internal static void Init() => VariantRegistry.Register(Order, InvVar);
#pragma warning restore CA2255
}
