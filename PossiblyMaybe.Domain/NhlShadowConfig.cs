namespace PossiblyMaybe.Domain;

/// <summary>
/// NHL totals — goals EMA α=0.18, same gate shape as FAIL soccer shadows
/// (w=0.40, |exp−line|≥0.25, EV≥4%). Tier A runnable after T2a O→C; verdicts stay
/// shadow / radar only (no paper from sim). Live candidates feed the shadow ledger.
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
