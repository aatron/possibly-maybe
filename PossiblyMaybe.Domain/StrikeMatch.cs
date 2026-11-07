namespace PossiblyMaybe.Domain;

/// <summary>
/// Venue totals strike must sit near our model line or we skip (wrong-line GO risk).
/// NBA half-points → 0.5; soccer quarter-goals → 0.25.
/// </summary>
public static class StrikeMatch
{
    public const double MaxNbaDelta = 0.5;
    public const double MaxSoccerDelta = 0.25;

    public static double MaxDelta(string sport)
        => sport.Equals("NBA", StringComparison.OrdinalIgnoreCase) ? MaxNbaDelta : MaxSoccerDelta;

    public static bool WithinMaxDelta(string sport, double strike, double modelLine)
        => Math.Abs(strike - modelLine) <= MaxDelta(sport);
}
