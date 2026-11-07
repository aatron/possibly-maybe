namespace PossiblyMaybe.Domain;

/// <summary>
/// NHL totals — shadow only until free multi-season O→C exists (A4 census).
/// Goals EMA α=0.18, same gate shape as FAIL soccer shadows (w=0.40, |exp−line|≥0.25, EV≥4%).
/// Not a money path; live candidates feed the shadow ledger / wave radar via line_tracker.
/// </summary>
public static class NhlShadowConfig
{
    public const string Market = "TOTAL";
    public const string Model = "NhlGoalsEma";
    public const double MarketWeight = 0.40;
    public const double MinEv = 0.04;
    public const double MinAbsTotalEdge = 0.25;
    public const double EmaAlpha = 0.18;
    public const double BankrollSeed = 1000;
    /// <summary>League-mean goals per side (~6 total / 2).</summary>
    public const double LeagueInit = 3.0;
}
