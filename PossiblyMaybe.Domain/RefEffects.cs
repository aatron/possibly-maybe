namespace PossiblyMaybe.Domain;

/// <summary>Walk-forward ref crew home-win residual vs league baseline (fit excludes Donaghy-flagged rows).</summary>
public sealed class RefEffects
{
    private readonly Dictionary<string, (double Sum, int N)> _homeWin = new(StringComparer.OrdinalIgnoreCase);
    private double _leagueSum;
    private int _leagueN;

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

    /// <summary>Average (ref home-win rate − league rate); 0 if unknown/sparse.</summary>
    public double CrewHomeBias(IEnumerable<string> officials, int minGames = 20)
    {
        var league = _leagueN == 0 ? 0.5 : _leagueSum / _leagueN;
        double sum = 0;
        var n = 0;
        foreach (var name in officials)
        {
            if (!_homeWin.TryGetValue(name, out var s) || s.N < minGames) continue;
            sum += s.Sum / s.N - league;
            n++;
        }
        return n == 0 ? 0 : sum / n;
    }
}
