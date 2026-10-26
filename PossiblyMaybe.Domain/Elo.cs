namespace PossiblyMaybe.Domain;

public sealed class EloBook
{
    private readonly Dictionary<string, double> _rating = new(StringComparer.Ordinal);
    private readonly double _k;
    private readonly double _homeAdvantage;

    public EloBook(double initial = 1500, double k = 20, double homeAdvantage = 100)
    {
        Initial = initial;
        _k = k;
        _homeAdvantage = homeAdvantage;
    }

    public double Initial { get; }

    public double Rating(string team)
        => _rating.TryGetValue(team, out var r) ? r : Initial;

    public double HomeWinProbability(string home, string away)
    {
        var diff = Rating(home) + _homeAdvantage - Rating(away);
        return 1.0 / (1.0 + Math.Pow(10, -diff / 400.0));
    }

    public void Update(string home, string away, bool homeWon)
    {
        var pHome = HomeWinProbability(home, away);
        var aHome = homeWon ? 1.0 : 0.0;
        var rHome = Rating(home);
        var rAway = Rating(away);
        _rating[home] = rHome + _k * (aHome - pHome);
        _rating[away] = rAway + _k * ((1.0 - aHome) - (1.0 - pHome));
    }
}
