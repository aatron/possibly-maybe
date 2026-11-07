using System.Runtime.CompilerServices;

namespace PossiblyMaybe.Domain;

/// <summary>
/// Soccer distribution curves over stored λ (B4): mls-dc, +poisson-all, +negbin, +dc-rho.
/// Means stay on the candidate; only P(over) / league scope changes.
/// </summary>
public static class SoccerDistFamily
{
    public const int Order = 130;

    /// <summary>Locked NB2 φ from MoM on a mild-overdispersion train prior (φ on train in Tier A folds).</summary>
    public const double TrainPhi = 0.12;

    /// <summary>Locked Dixon–Coles ρ (low-score correction); re-fit on train only in Tier A.</summary>
    public const double TrainRho = -0.10;

    private static readonly HashSet<string> MlsOnly = new(StringComparer.OrdinalIgnoreCase) { "MLS" };

    /// <summary>MLS-only + independent Poisson P(over) (DC skeleton on stored λ; DC means need alt candidate later).</summary>
    public static Variant MlsDc { get; } = new(
        "mls-dc", "mls-dixon-coles-poisson",
        NbaOverProbKind.NormalSigma,
        false, VariantRegistry.DefaultEdgeCapEv, false,
        VariantRegistry.DefaultNbaLineMoveK, VariantRegistry.DefaultSoccerLineMoveK,
        false, false, MlsOnly,
        SoccerOverProb: SoccerOverProbKind.PoissonAll);

    /// <summary>Force Poisson CDF for every soccer league (MLS / Liga MX leave logistic).</summary>
    public static Variant PoissonAll { get; } = new(
        "+poisson-all", "soccer-poisson-all-leagues",
        NbaOverProbKind.NormalSigma,
        false, VariantRegistry.DefaultEdgeCapEv, false,
        VariantRegistry.DefaultNbaLineMoveK, VariantRegistry.DefaultSoccerLineMoveK,
        false, false, null,
        SoccerOverProb: SoccerOverProbKind.PoissonAll);

    /// <summary>NB2 total with φ locked from train MoM (<see cref="TrainPhi"/> / <see cref="TotalsMath.FitNegBinPhi"/>).</summary>
    public static Variant NegBin { get; } = new(
        "+negbin", "soccer-negbin-phi",
        NbaOverProbKind.NormalSigma,
        false, VariantRegistry.DefaultEdgeCapEv, false,
        VariantRegistry.DefaultNbaLineMoveK, VariantRegistry.DefaultSoccerLineMoveK,
        false, false, null,
        SoccerOverProb: SoccerOverProbKind.NegBin, SoccerPhi: TrainPhi);

    /// <summary>Dixon–Coles τ_ρ joint on stored λ (<see cref="DixonColesBook.OverProbWithRho"/>).</summary>
    public static Variant DcRho { get; } = new(
        "+dc-rho", "soccer-dc-rho",
        NbaOverProbKind.NormalSigma,
        false, VariantRegistry.DefaultEdgeCapEv, false,
        VariantRegistry.DefaultNbaLineMoveK, VariantRegistry.DefaultSoccerLineMoveK,
        false, false, null,
        SoccerOverProb: SoccerOverProbKind.DcRho, SoccerRho: TrainRho);

#pragma warning disable CA2255 // family self-register so wave-B is one file
    [ModuleInitializer]
    internal static void Init() => VariantRegistry.Register(Order, MlsDc, PoissonAll, NegBin, DcRho);
#pragma warning restore CA2255
}
