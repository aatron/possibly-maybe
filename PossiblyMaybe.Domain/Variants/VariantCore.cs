namespace PossiblyMaybe.Domain;

/// <summary>
/// Immutable versioned betting config. Changing behavior = register a new Id; never edit old ids.
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
    /// <summary>B2: shrink model toward league mean on early-season / promoted phases.</summary>
    bool Pooling = false,
    /// <summary>B2: shrink + widen σ when <see cref="SlateCandidate.RosterFlag"/> is set.</summary>
    bool RosterReset = false,
    /// <summary>When true, blend model+line with inverse-variance (see <see cref="BlendFamily"/>).</summary>
    bool InvVarBlend = false,
    /// <summary>When true, add <see cref="SlateCandidate.RefTotalsResidual"/> to model exp (refs-nba).</summary>
    bool UseRefTotals = false);

public enum NbaOverProbKind
{
    NormalSigma = 0,
    Logistic = 1,
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
    /// <summary>Crew foul/pace residual pts from <see cref="RefEffects"/>; used when variant has UseRefTotals.</summary>
    double RefTotalsResidual = 0);

public sealed record VariantDecision(
    bool Pick,
    string? Side,
    double? Line,
    int AmericanOdds,
    double ModelProb,
    double Ev,
    double AbsEdge,
    string? SkipReason);

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
}

/// <summary>
/// Launch-set registry. Families register via <see cref="Register"/> (module initializers);
/// wave-B adds one family file — no edit to other families.
/// </summary>
public static class VariantRegistry
{
    public const string V0Id = "V0";
    // Align with W2 Tier A picks in Guards.cs (NBA edge-cap alone fails proof; still used for soccer + live absurds).
    public const double DefaultEdgeCapEv = Guards.RecommendedEdgeCap;
    public const double DefaultNbaLineMoveK = Guards.RecommendedNbaLineMoveK;
    public const double DefaultSoccerLineMoveK = Guards.RecommendedSoccerLineMoveK;

    private static readonly object Gate = new();
    private static readonly List<(int Order, Variant[] Variants)> Bags = [];
    private static IReadOnlyList<Variant>? _all;

    /// <summary>Family files call this from a <c>[ModuleInitializer]</c>. Lower <paramref name="order"/> first.</summary>
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

    /// <param name="pausedLeagues">
    /// Sports currently paused by rolling-CLV shutoff (from <see cref="Guards.ShutoffSignalVariantId"/> shadow).
    /// Ignored unless <see cref="Variant.LeagueShutoff"/> is true.
    /// </param>
    public static VariantDecision Evaluate(
        Variant v, SlateCandidate c, IReadOnlySet<string>? pausedLeagues = null)
    {
        if (OregonExclusion.IsExcluded(c.Sport, c.Home, c.Away))
            return Skip(VariantSkip.Excluded);
        if (c.Stale)
            return Skip(VariantSkip.Stale);
        if (v.Leagues is not null && !v.Leagues.Contains(c.Sport))
            return Skip(VariantSkip.LeagueOff);
        if (v.LeagueShutoff
            && pausedLeagues is not null
            && pausedLeagues.Contains(c.Sport))
            return Skip(VariantSkip.LeaguePaused);
        if (c.Line is not { } line)
            return Skip(VariantSkip.NoLine);

        c = PoolingFamily.Apply(v, c);
        var exp = v.BlendedExp(c, line);
        var edge = exp - line;
        if (Math.Abs(edge) < c.MinAbsEdge)
            return Skip(VariantSkip.NoEdge);

        var betOver = edge > 0;
        var side = betOver ? "OVER" : "UNDER";
        var pOver = OverProb(v, c, exp, line);
        var p = betOver ? pOver : 1.0 - pOver;
        var juice = Juice(v, c, betOver);
        var ev = OddsMath.ExpectedValue(p, juice);

        if (ev < c.MinEv)
            return new VariantDecision(false, side, line, juice, p, ev, Math.Abs(edge), VariantSkip.LowEv);
        if (v.EdgeCap && ev > v.EdgeCapEv)
            return new VariantDecision(false, side, line, juice, p, ev, Math.Abs(edge), VariantSkip.EdgeCap);
        if (v.LineMove && LineMovedAgainst(c, betOver, v))
            return new VariantDecision(false, side, line, juice, p, ev, Math.Abs(edge), VariantSkip.LineMove);

        return new VariantDecision(true, side, line, juice, p, ev, Math.Abs(edge), null);
    }

    private static double BlendedExp(this Variant v, SlateCandidate c, double line)
    {
        var model = c.ModelExp + (v.UseRefTotals ? c.RefTotalsResidual : 0);
        if (v.InvVarBlend)
        {
            var (sm, sl) = BlendFamily.LockedSigmas(c.Sport);
            return MarketBlend.InverseVariance(model, line, sm, sl);
        }

        return c.MarketWeight * line + (1 - c.MarketWeight) * model;
    }

    private static double OverProb(Variant v, SlateCandidate c, double blendedExp, double line)
    {
        if (c.Sport.Equals("NBA", StringComparison.OrdinalIgnoreCase))
        {
            return v.NbaOverProb == NbaOverProbKind.NormalSigma
                ? TotalsMath.NormalOverProb(blendedExp, line, c.NbaSigma)
                : TotalsMath.OverProb(blendedExp, line, sharpness: 0.25);
        }

        var lh = c.LambdaHome ?? blendedExp / 2;
        var la = c.LambdaAway ?? blendedExp / 2;
        return SoccerOverProbConfig.OverProb(lh, la, blendedExp, line, c.Sport);
    }

    private static int Juice(Variant v, SlateCandidate c, bool betOver)
    {
        if (!v.UseRealJuice) return -110;
        var raw = betOver ? c.DkOverAmerican : c.DkUnderAmerican;
        return raw is null or 0 ? -110 : raw.Value;
    }

    private static bool LineMovedAgainst(SlateCandidate c, bool betOver, Variant v)
    {
        if (c.OpenLine is not { } open || c.Line is not { } cur) return false;
        var move = cur - open; // + = line rose
        var k = c.Sport.Equals("NBA", StringComparison.OrdinalIgnoreCase) ? v.NbaLineMoveK : v.SoccerLineMoveK;
        // OVER hurt when line drops; UNDER hurt when line rises
        return betOver ? move <= -k : move >= k;
    }

    private static VariantDecision Skip(string reason)
        => new(false, null, null, -110, 0, 0, 0, reason);
}
