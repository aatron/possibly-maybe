namespace PossiblyMaybe.Domain;

/// <summary>
/// Walk-forward ref crew residuals vs league baseline (fit excludes Donaghy-flagged rows).
/// Home-win bias for ML Gate 1b; totals / foul-pace residual for <c>refs-nba</c>.
/// </summary>
public sealed class RefEffects
{
    private readonly Dictionary<string, (double Sum, int N)> _homeWin = new(StringComparer.OrdinalIgnoreCase);
    private double _leagueSum;
    private int _leagueN;

    // shortcut: total-pts residual stands in for foul/pace (no PF column on GameBox); upgrade when PF rates land.
    private readonly Dictionary<string, (double Sum, int N)> _totals = new(StringComparer.OrdinalIgnoreCase);
    private double _leagueTotalSum;
    private int _leagueTotalN;

    private readonly Dictionary<string, (double Sum, int N)> _pace = new(StringComparer.OrdinalIgnoreCase);
    private double _leaguePaceSum;
    private int _leaguePaceN;

    public void Observe(IEnumerable<string> officials, bool homeWon, bool excludeFromFit)
    {
        if (excludeFromFit) return;
        var y = homeWon ? 1.0 : 0.0;
        _leagueSum += y;
        _leagueN++;
        foreach (var name in officials)
        {
            _homeWin.TryGetValue(name, out var s);
            _homeWin[name] = (s.Sum + y, s.N + 1);
        }
    }

    /// <summary>
    /// Fit crew mean total (and optional possessions as pace/foul proxy via 0.44·FTA in poss).
    /// </summary>
    public void ObserveTotal(
        IEnumerable<string> officials, double totalPts, bool excludeFromFit, double? possessions = null)
    {
        if (excludeFromFit) return;
        _leagueTotalSum += totalPts;
        _leagueTotalN++;
        foreach (var name in officials)
        {
            _totals.TryGetValue(name, out var s);
            _totals[name] = (s.Sum + totalPts, s.N + 1);
        }

        if (possessions is not { } poss) return;
        _leaguePaceSum += poss;
        _leaguePaceN++;
        foreach (var name in officials)
        {
            _pace.TryGetValue(name, out var s);
            _pace[name] = (s.Sum + poss, s.N + 1);
        }
    }

    /// <summary>Average (ref home-win rate − league rate); 0 if unknown/sparse.</summary>
    public double CrewHomeBias(IEnumerable<string> officials, int minGames = 20)
        => CrewMeanBias(_homeWin, _leagueSum, _leagueN, officials, minGames, leagueFallback: 0.5);

    /// <summary>Average (ref mean total − league mean total); 0 if unknown/sparse.</summary>
    public double CrewTotalsBias(IEnumerable<string> officials, int minGames = 20)
        => CrewMeanBias(_totals, _leagueTotalSum, _leagueTotalN, officials, minGames, leagueFallback: 0);

    /// <summary>Average (ref mean possessions − league); 0 if unknown/sparse.</summary>
    public double CrewPaceBias(IEnumerable<string> officials, int minGames = 20)
        => CrewMeanBias(_pace, _leaguePaceSum, _leaguePaceN, officials, minGames, leagueFallback: 0);

    /// <summary>
    /// Foul/pace residual in points. Prefer total-pts bias; when only pace was fit, scale possessions.
    /// </summary>
    public double CrewFoulPaceResidual(
        IEnumerable<string> officials, double pacePtsPerPoss = 2.0, int minGames = 20)
    {
        if (_leagueTotalN > 0) return CrewTotalsBias(officials, minGames);
        return pacePtsPerPoss * CrewPaceBias(officials, minGames);
    }

    private static double CrewMeanBias(
        Dictionary<string, (double Sum, int N)> byRef,
        double leagueSum, int leagueN,
        IEnumerable<string> officials, int minGames, double leagueFallback)
    {
        var league = leagueN == 0 ? leagueFallback : leagueSum / leagueN;
        double sum = 0;
        var n = 0;
        foreach (var name in officials)
        {
            if (!byRef.TryGetValue(name, out var s) || s.N < minGames) continue;
            sum += s.Sum / s.N - league;
            n++;
        }
        return n == 0 ? 0 : sum / n;
    }
}
