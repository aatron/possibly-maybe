namespace PossiblyMaybe.Domain;

/// <summary>
/// Immutable versioned betting config. Changing behavior = register a new Id; never edit old ids.
/// Optional flags default false/null so wave-B families can add one file without touching others.
/// </summary>
public sealed record Variant(
    string Id,
    string Label,
    NbaOverProbKind NbaOverProb,
    bool EdgeCap,
    double EdgeCapEv,
    bool LineMove,
    double NbaLineMoveK,
    double SoccerLineMoveK,
    bool LeagueShutoff,
    bool UseRealJuice,
    /// <summary>null = all paper leagues; otherwise only these sports.</summary>
    IReadOnlySet<string>? Leagues,
    bool Pooling = false,
    bool RosterReset = false,
    bool InvVarBlend = false,
    bool UseRefTotals = false,
    bool AbsenceAdjust = false,
    bool InjuryGuard = false,
    double? MarketWeight = null,
    double? MinAbsEdge = null,
    double? MinEv = null,
    NbaTotalsModel NbaModel = NbaTotalsModel.PaceEff,
    SoccerOverProbKind SoccerOverProb = SoccerOverProbKind.LeagueDefault,
    double SoccerPhi = 0,
    double SoccerRho = 0,
    bool FittedCurve = false,
    bool ConsensusOutlier = false,
    double ConsensusAlpha = 0.05,
    bool LineMoveFollow = false,
    bool ConformalFilter = false,
    bool UseKalmanRatings = false);

public enum NbaOverProbKind
{
    NormalSigma = 0,
    Logistic = 1,
}

public enum NbaTotalsModel
{
    PaceEff = 0,
    PaceEffEfg = 1,
}

public enum SoccerOverProbKind
{
    LeagueDefault = 0,
    PoissonAll = 1,
    NegBin = 2,
    DcRho = 3,
}

/// <summary>One game's shared model/line inputs; variants filter it.</summary>
public sealed record SlateCandidate(
    string Sport,
    string Yyyymmdd,
    string Home,
    string Away,
    double ModelExp,
    double? LambdaHome,
    double? LambdaAway,
    double? Line,
    double? OpenLine,
    int? DkOverAmerican,
    int? DkUnderAmerican,
    double NbaSigma,
    double MarketWeight,
    double MinAbsEdge,
    double MinEv,
    bool Stale,
    bool? InjuryFlag = null,
    bool? RosterFlag = null,
    string? SeasonPhase = null,
    double RefTotalsResidual = 0,
    double AbsenceAdjPts = 0,
    double? ModelExpEfg = null,
    double? KalshiOverAsk = null,
    double? KalshiUnderAsk = null,
    double? CryptoOverAsk = null,
    double? CryptoUnderAsk = null,
    double? ConformalHalfWidth = null,
    double? KalmanModelExp = null,
    /// <summary>TOTAL (default), SPREAD, or ML. Same GameKey may have one row per market.</summary>
    string Market = "TOTAL");

public sealed record VariantDecision(
    bool Pick,
    string? Side,
    double? Line,
    int AmericanOdds,
    double ModelProb,
    double Ev,
    double AbsEdge,
    string? SkipReason,
    /// <summary>True when AmericanOdds fell back to −110 (no real venue price).</summary>
    bool AssumedPrice = false);

public static class VariantSkip
{
    public const string Excluded = "EXCLUDED";
    public const string EdgeCap = "EDGE_CAP";
    public const string LineMove = "LINE_MOVE";
    public const string LeaguePaused = "LEAGUE_PAUSED";
    public const string LeagueOff = "LEAGUE_OFF";
    public const string Stale = "STALE";
    public const string NoLine = "NO_LINE";
    public const string NoEdge = "NO_EDGE";
    public const string LowEv = "LOW_EV";
    /// <summary>Launch-set variants are totals filters; SPREAD/ML rows stay stored only.</summary>
    public const string WrongMarket = "WRONG_MARKET";
    public const string Injury = "INJURY";
    public const string NoFollow = "NO_FOLLOW";
    public const string NoVenueAsks = "NO_VENUE_ASKS";
    public const string NoOutlier = "NO_OUTLIER";
    public const string Conformal = "CONFORMAL";
}

/// <summary>
/// Launch-set registry. Families register via <see cref="Register"/> (module initializers);
/// wave-B adds one family file — no edit to other families.
/// </summary>
public static class VariantRegistry
{
    public const string V0Id = "V0";
    /// <summary>Bump when registry shape / Evaluate contract changes (R2 decision log).</summary>
    public const int RegistryVersion = 1;
    public const double DefaultEdgeCapEv = Guards.RecommendedEdgeCap;
    public const double DefaultNbaLineMoveK = Guards.RecommendedNbaLineMoveK;
    public const double DefaultSoccerLineMoveK = Guards.RecommendedSoccerLineMoveK;

    private static readonly object Gate = new();
    private static readonly List<(int Order, Variant[] Variants)> Bags = [];
    private static IReadOnlyList<Variant>? _all;

    public static void Register(int order, params Variant[] variants)
    {
        lock (Gate)
        {
            Bags.Add((order, variants));
            _all = null;
        }
    }

    public static Variant V0 => ChampFamily.V0;

    public static IReadOnlyList<Variant> All
    {
        get
        {
            lock (Gate)
            {
                return _all ??= Bags.OrderBy(b => b.Order).SelectMany(b => b.Variants).ToList();
            }
        }
    }

    public static Variant Require(string id)
        => All.FirstOrDefault(v => v.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
           ?? throw new ArgumentException($"Unknown variant id '{id}'");

    public static string GameKey(SlateCandidate c) => $"{c.Yyyymmdd} {c.Away}@{c.Home}";

    public static VariantDecision Evaluate(
        Variant v, SlateCandidate c, IReadOnlySet<string>? pausedLeagues = null)
    {
        if (OregonExclusion.IsExcluded(c.Sport, c.Home, c.Away))
            return Skip(VariantSkip.Excluded);
        // Launch-set Evaluate is totals O/U; C7 SPREAD/ML candidates are store-only until a market family lands.
        if (!c.Market.Equals("TOTAL", StringComparison.OrdinalIgnoreCase))
            return Skip(VariantSkip.WrongMarket);
        if (c.Stale)
            return Skip(VariantSkip.Stale);
        if (v.Leagues is not null && !v.Leagues.Contains(c.Sport))
            return Skip(VariantSkip.LeagueOff);
        if (v.LeagueShutoff
            && pausedLeagues is not null
            && pausedLeagues.Contains(c.Sport))
            return Skip(VariantSkip.LeaguePaused);
        if (v.InjuryGuard && c.InjuryFlag == true)
            return Skip(VariantSkip.Injury);
        if (c.Line is not { } line)
            return Skip(VariantSkip.NoLine);

        c = PoolingFamily.Apply(v, c);
        var exp = v.BlendedExp(c, line);
        var edge = exp - line;
        var minEdge = v.MinAbsEdge ?? c.MinAbsEdge;
        if (Math.Abs(edge) < minEdge)
            return Skip(VariantSkip.NoEdge);

        var betOver = edge > 0;
        var side = betOver ? "OVER" : "UNDER";
        var pOver = OverProb(v, c, exp, line);
        var p = betOver ? pOver : 1.0 - pOver;
        var (juice, assumed) = ResolveJuice(v, c, betOver);
        var ev = OddsMath.ExpectedValue(p, juice);

        var minEv = v.MinEv ?? c.MinEv;
        if (ev < minEv)
            return new VariantDecision(false, side, line, juice, p, ev, Math.Abs(edge), VariantSkip.LowEv, assumed);
        if (v.EdgeCap && ev > v.EdgeCapEv)
            return new VariantDecision(false, side, line, juice, p, ev, Math.Abs(edge), VariantSkip.EdgeCap, assumed);
        if (v.LineMove && LineMovedAgainst(c, betOver, v))
            return new VariantDecision(false, side, line, juice, p, ev, Math.Abs(edge), VariantSkip.LineMove, assumed);
        if (v.LineMoveFollow && !LineMovedToward(c, betOver, v))
            return new VariantDecision(false, side, line, juice, p, ev, Math.Abs(edge), VariantSkip.NoFollow, assumed);
        if (v.ConsensusOutlier)
        {
            var odds = VenueDecimalOdds(c, betOver);
            if (odds.Count < 2)
                return new VariantDecision(false, side, line, juice, p, ev, Math.Abs(edge), VariantSkip.NoVenueAsks, assumed);
            if (!MarketStructureFamily.IsSoftOutlier(odds, v.ConsensusAlpha))
                return new VariantDecision(false, side, line, juice, p, ev, Math.Abs(edge), VariantSkip.NoOutlier, assumed);
        }
        if (v.ConformalFilter)
        {
            var hw = c.ConformalHalfWidth ?? HigherRiskFamily.GaussianHalfWidth(c.NbaSigma);
            if (hw is null || !HigherRiskFamily.LineExcludedByInterval(exp, line, hw.Value))
                return new VariantDecision(false, side, line, juice, p, ev, Math.Abs(edge), VariantSkip.Conformal, assumed);
        }

        return new VariantDecision(true, side, line, juice, p, ev, Math.Abs(edge), null, assumed);
    }

    private static double BlendedExp(this Variant v, SlateCandidate c, double line)
    {
        var model = v.UseKalmanRatings && c.KalmanModelExp is { } kExp
            ? kExp
            : v.NbaModel == NbaTotalsModel.PaceEffEfg
                ? c.ModelExpEfg ?? c.ModelExp
                : c.ModelExp;
        model += v.UseRefTotals ? c.RefTotalsResidual : 0;
        model += v.AbsenceAdjust ? c.AbsenceAdjPts : 0;

        var w = v.MarketWeight ?? c.MarketWeight;
        if (v.InvVarBlend)
        {
            var (sm, sl) = BlendFamily.LockedSigmas(c.Sport);
            return MarketBlend.InverseVariance(model, line, sm, sl);
        }

        return w * line + (1 - w) * model;
    }

    private static double OverProb(Variant v, SlateCandidate c, double blendedExp, double line)
    {
        if (v.FittedCurve)
            return TotalsMath.NormalOverProb(blendedExp, line, FittedCurves.SigmaFor(c.Sport));

        if (c.Sport.Equals("NBA", StringComparison.OrdinalIgnoreCase))
        {
            return v.NbaOverProb == NbaOverProbKind.NormalSigma
                ? TotalsMath.NormalOverProb(blendedExp, line, c.NbaSigma)
                : TotalsMath.OverProb(blendedExp, line, sharpness: 0.25);
        }

        if (c.Sport.Equals("NCAAB", StringComparison.OrdinalIgnoreCase)
            || c.Sport.Equals("NCAAF", StringComparison.OrdinalIgnoreCase)
            || c.Sport.Equals("NFL", StringComparison.OrdinalIgnoreCase)
            || c.Sport.Equals("MLB", StringComparison.OrdinalIgnoreCase))
            return TotalsMath.OverProb(blendedExp, line, sharpness: 0.2);

        var lh = c.LambdaHome ?? blendedExp / 2;
        var la = c.LambdaAway ?? blendedExp / 2;
        return v.SoccerOverProb switch
        {
            SoccerOverProbKind.PoissonAll => TotalsMath.PoissonTotalOverProb(lh, la, line),
            SoccerOverProbKind.NegBin => TotalsMath.NegBinTotalOverProb(lh + la, v.SoccerPhi, line),
            SoccerOverProbKind.DcRho => DixonColesBook.OverProbWithRho(lh, la, line, v.SoccerRho),
            _ => SoccerOverProbConfig.OverProb(lh, la, blendedExp, line, c.Sport),
        };
    }

    /// <summary>DK american for the side; Assumed=true when falling back to −110.</summary>
    public static (int American, bool Assumed) ResolveJuice(Variant v, SlateCandidate c, bool betOver)
    {
        if (!v.UseRealJuice) return (-110, true);
        var raw = betOver ? c.DkOverAmerican : c.DkUnderAmerican;
        return raw is null or 0 ? (-110, true) : (raw.Value, false);
    }

    /// <summary>Paper champ path: always prefer real DK juice when present (R6).</summary>
    public static Variant PaperChamp => V0 with { UseRealJuice = true };

    private static bool LineMovedAgainst(SlateCandidate c, bool betOver, Variant v)
    {
        if (c.OpenLine is not { } open || c.Line is not { } cur) return false;
        var move = cur - open;
        var k = c.Sport.Equals("NBA", StringComparison.OrdinalIgnoreCase)
                || c.Sport.Equals("NCAAB", StringComparison.OrdinalIgnoreCase)
            ? v.NbaLineMoveK
            : v.SoccerLineMoveK;
        return betOver ? move <= -k : move >= k;
    }

    private static bool LineMovedToward(SlateCandidate c, bool betOver, Variant v)
    {
        if (c.OpenLine is not { } open || c.Line is not { } cur) return false;
        var move = cur - open;
        var k = c.Sport.Equals("NBA", StringComparison.OrdinalIgnoreCase)
                || c.Sport.Equals("NCAAB", StringComparison.OrdinalIgnoreCase)
            ? v.NbaLineMoveK
            : v.SoccerLineMoveK;
        return betOver ? move >= k : move <= -k;
    }

    private static List<double> VenueDecimalOdds(SlateCandidate c, bool betOver)
    {
        var list = new List<double>(4);
        var am = betOver ? c.DkOverAmerican : c.DkUnderAmerican;
        if (am is int a and not 0)
            list.Add(OddsMath.AmericanToDecimal(a));
        var kAsk = betOver ? c.KalshiOverAsk : c.KalshiUnderAsk;
        if (kAsk is > 0 and < 1)
            list.Add(1.0 / kAsk.Value);
        var cAsk = betOver ? c.CryptoOverAsk : c.CryptoUnderAsk;
        if (cAsk is > 0 and < 1)
            list.Add(1.0 / cAsk.Value);
        return list;
    }

    private static VariantDecision Skip(string reason)
        => new(false, null, null, -110, 0, 0, 0, reason);
}
