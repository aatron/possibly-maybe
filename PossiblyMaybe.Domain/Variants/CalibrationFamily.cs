using System.Runtime.CompilerServices;

namespace PossiblyMaybe.Domain;

/// <summary>
/// Locked residual σ for <see cref="Variant.FittedCurve"/> (pre-holdout; eval_ml_general).
/// Not re-tuned on look-once seasons.
/// </summary>
public static class FittedCurves
{
    public const double NbaSigma = ExpandingResidualSigma.FallbackSigma; // 22.8
    public const double MlsSigma = 1.65;
    public const double Ligue1Sigma = 1.68;
    public const double LigaMxSigma = 1.70;
    public const double SerieASigma = 1.62;
    public const double SoccerDefaultSigma = 1.65;

    public static double SigmaFor(string sport)
        => sport.ToUpperInvariant() switch
        {
            "NBA" => NbaSigma,
            "MLS" => MlsSigma,
            "LIGUE1" => Ligue1Sigma,
            "LIGAMX" => LigaMxSigma,
            "SERIEA" => SerieASigma,
            _ => SoccerDefaultSigma,
        };
}

/// <summary>B7 calibration / filter stack: fitted curves, strict gates, EV bands.</summary>
public static class CalibrationFamily
{
    public const int Order = 170;

    public const double StrictEvMin = 0.04;
    public const double StrictEvMax = 0.20;
    public const double SoccerStrictEdge = 0.5;
    public const double NbaStrictEdge = 8.0;

    private static readonly HashSet<string> NbaOnly = new(StringComparer.OrdinalIgnoreCase) { "NBA" };
    private static readonly HashSet<string> MlsOnly = new(StringComparer.OrdinalIgnoreCase) { "MLS" };
    private static readonly HashSet<string> Ligue1Only = new(StringComparer.OrdinalIgnoreCase) { "LIGUE1" };
    private static readonly HashSet<string> LigaMxOnly = new(StringComparer.OrdinalIgnoreCase) { "LIGAMX" };
    private static readonly HashSet<string> SerieAOnly = new(StringComparer.OrdinalIgnoreCase) { "SERIEA" };
    private static readonly HashSet<string> SoccerLeagues = new(StringComparer.OrdinalIgnoreCase)
    {
        "MLS", "LIGUE1", "LIGAMX", "SERIEA",
    };

    public static Variant VFcNba { get; } = Fitted("V-FC-NBA", "+fitted-curve-nba", NbaOnly);
    public static Variant VFcMls { get; } = Fitted("V-FC-MLS", "+fitted-curve-mls", MlsOnly);
    public static Variant VFcLigue1 { get; } = Fitted("V-FC-LIGUE1", "+fitted-curve-ligue1", Ligue1Only);
    public static Variant VFcLigaMx { get; } = Fitted("V-FC-LIGAMX", "+fitted-curve-ligamx", LigaMxOnly);
    public static Variant VFcSerieA { get; } = Fitted("V-FC-SERIEA", "+fitted-curve-seriea", SerieAOnly);

    public static Variant VSoccerStrict { get; } = new(
        "V-SOC-STRICT", "soccer-strict",
        NbaOverProbKind.NormalSigma,
        EdgeCap: true, EdgeCapEv: StrictEvMax,
        LineMove: false,
        VariantRegistry.DefaultNbaLineMoveK, VariantRegistry.DefaultSoccerLineMoveK,
        LeagueShutoff: false, UseRealJuice: false, Leagues: SoccerLeagues,
        MinAbsEdge: SoccerStrictEdge, MinEv: StrictEvMin);

    public static Variant VNbaStrict { get; } = new(
        "V-NBA-STRICT", "nba-strict",
        NbaOverProbKind.NormalSigma,
        EdgeCap: true, EdgeCapEv: StrictEvMax,
        LineMove: false,
        VariantRegistry.DefaultNbaLineMoveK, VariantRegistry.DefaultSoccerLineMoveK,
        LeagueShutoff: false, UseRealJuice: false, Leagues: NbaOnly,
        MinAbsEdge: NbaStrictEdge, MinEv: StrictEvMin);

    public static Variant VEv0410 { get; } = EvBand("V-EV-04-10", "ev-band-4-10", 0.04, 0.10);
    public static Variant VEv1020 { get; } = EvBand("V-EV-10-20", "ev-band-10-20", 0.10, 0.20);
    public static Variant VEv0420 { get; } = EvBand("V-EV-04-20", "ev-band-4-20", 0.04, 0.20);

#pragma warning disable CA2255 // family self-register so wave-B is one file
    [ModuleInitializer]
    internal static void Init() => VariantRegistry.Register(
        Order,
        VFcNba, VFcMls, VFcLigue1, VFcLigaMx, VFcSerieA,
        VSoccerStrict, VNbaStrict,
        VEv0410, VEv1020, VEv0420);
#pragma warning restore CA2255

    private static Variant Fitted(string id, string label, IReadOnlySet<string> leagues) => new(
        id, label,
        NbaOverProbKind.NormalSigma,
        EdgeCap: false, EdgeCapEv: VariantRegistry.DefaultEdgeCapEv,
        LineMove: false,
        VariantRegistry.DefaultNbaLineMoveK, VariantRegistry.DefaultSoccerLineMoveK,
        LeagueShutoff: false, UseRealJuice: false, Leagues: leagues,
        FittedCurve: true);

    private static Variant EvBand(string id, string label, double minEv, double maxEv) => new(
        id, label,
        NbaOverProbKind.NormalSigma,
        EdgeCap: true, EdgeCapEv: maxEv,
        LineMove: false,
        VariantRegistry.DefaultNbaLineMoveK, VariantRegistry.DefaultSoccerLineMoveK,
        LeagueShutoff: false, UseRealJuice: false, Leagues: null,
        MinEv: minEv);
}
