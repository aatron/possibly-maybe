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
    IReadOnlySet<string>? Leagues);

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
    bool Stale);

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

/// <summary>Launch set for round 3 shadow ledger. Ids are permanent.</summary>
public static class VariantRegistry
{
    public const string V0Id = "V0";
    // Align with W2 Tier A picks in Guards.cs (NBA edge-cap alone fails proof; still used for soccer + live absurds).
    public const double DefaultEdgeCapEv = Guards.RecommendedEdgeCap;
    public const double DefaultNbaLineMoveK = Guards.RecommendedNbaLineMoveK;
    public const double DefaultSoccerLineMoveK = Guards.RecommendedSoccerLineMoveK;

    private static readonly HashSet<string> NoSerieA = new(StringComparer.OrdinalIgnoreCase)
    {
        "NBA", "MLS", "LIGUE1", "LIGAMX",
    };

    public static Variant V0 { get; } = new(
        V0Id, "champ-2026-11-07",
        NbaOverProbKind.NormalSigma,
        EdgeCap: false, EdgeCapEv: DefaultEdgeCapEv,
        LineMove: false, NbaLineMoveK: DefaultNbaLineMoveK, SoccerLineMoveK: DefaultSoccerLineMoveK,
        LeagueShutoff: false, UseRealJuice: false, Leagues: null);

    public static IReadOnlyList<Variant> All { get; } =
    [
        V0,
        new("V0L", "legacy-logistic-nba",
            NbaOverProbKind.Logistic,
            false, DefaultEdgeCapEv, false, DefaultNbaLineMoveK, DefaultSoccerLineMoveK,
            false, false, null),
        new("V1", "+edgecap",
            NbaOverProbKind.NormalSigma,
            true, DefaultEdgeCapEv, false, DefaultNbaLineMoveK, DefaultSoccerLineMoveK,
            false, false, null),
        new("V2", "+linemove",
            NbaOverProbKind.NormalSigma,
            false, DefaultEdgeCapEv, true, DefaultNbaLineMoveK, DefaultSoccerLineMoveK,
            false, false, null),
        new("V3", "+edgecap+linemove",
            NbaOverProbKind.NormalSigma,
            true, DefaultEdgeCapEv, true, DefaultNbaLineMoveK, DefaultSoccerLineMoveK,
            false, false, null),
        // LeagueShutoff: pause sport when V3 rolling CLV (40) < 0; resume when ≥ 0.
        new("V4", "+shutoff",
            NbaOverProbKind.NormalSigma,
            true, DefaultEdgeCapEv, true, DefaultNbaLineMoveK, DefaultSoccerLineMoveK,
            true, false, null),
        new("V5", "+realjuice",
            NbaOverProbKind.NormalSigma,
            true, DefaultEdgeCapEv, true, DefaultNbaLineMoveK, DefaultSoccerLineMoveK,
            false, true, null),
        new("V-SA0", "no-seriea",
            NbaOverProbKind.NormalSigma,
            true, DefaultEdgeCapEv, true, DefaultNbaLineMoveK, DefaultSoccerLineMoveK,
            false, false, NoSerieA),
    ];

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

        var exp = v.MarketBlend(c, line);
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

    private static double MarketBlend(this Variant _, SlateCandidate c, double line)
        => c.MarketWeight * line + (1 - c.MarketWeight) * c.ModelExp;

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
