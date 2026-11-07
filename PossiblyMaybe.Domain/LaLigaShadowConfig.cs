namespace PossiblyMaybe.Domain;

/// <summary>
/// La Liga totals — shadow only (FAIL as paper champ). Goals EMA α=0.18, same gates as
/// FAIL-league retests in TimeDecaySweep (w=0.40, |exp−line|≥0.25, EV≥4%).
/// Not a money path; live candidates still feed the shadow ledger / wave radar.
/// </summary>
public static class LaLigaShadowConfig
{
    public const string Market = "TOTAL";
    public const string Model = "LaLigaGoalsEma";
    public const double MarketWeight = 0.40;
    public const double MinEv = 0.04;
    public const double MinAbsTotalEdge = 0.25;
    public const double EmaAlpha = 0.18;
    public const double BankrollSeed = 1000;
}
