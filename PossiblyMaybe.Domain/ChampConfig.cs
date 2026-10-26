namespace PossiblyMaybe.Domain;

/// <summary>
/// Money-path champ: NBA totals (O/U). Train-only max PnL in volume band (2026-10-25).
/// Blend 40% open total + 60% scoring EMA; bet when |exp−line|≥3.5 and EV≥4%.
/// Holdout 2024-25: 320 bets, +8.4u, +1.91pts CLV — paper until Gate 2b.
/// </summary>
public static class ChampConfig
{
    public const string Market = "TOTAL";
    public const string Model = "MarketScoring";
    public const double MarketWeight = 0.40;
    public const double MinEv = 0.04;
    public const double MinAbsTotalEdge = 3.5;
    public const double BankrollSeed = 1000;

    public const double AtsMinAbsMarginEdge = 4.5;
    public const double AtsMarketWeight = 0.7;
}
