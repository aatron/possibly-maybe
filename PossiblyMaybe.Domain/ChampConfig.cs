namespace PossiblyMaybe.Domain;

/// <summary>
/// Money-path champ: NBA totals via pace×ORtg (2026-10-25 experiment).
/// Blend 50% open + 50% PaceEfficiencyBook; |exp−line|≥6; EV≥4%.
/// Holdout 2024-25: 62 bets, +16.3u, +2.64pts CLV (beats prior Scoring +8.4u/320).
/// Rollback: git tag/commit 7be68f0 (Scoring w=0.4 edge=3.5).
/// </summary>
public static class ChampConfig
{
    public const string Market = "TOTAL";
    public const string Model = "PaceEff";
    public const double MarketWeight = 0.50;
    public const double MinEv = 0.04;
    public const double MinAbsTotalEdge = 6.0;
    public const double BankrollSeed = 1000;

    public const double AtsMinAbsMarginEdge = 4.5;
    public const double AtsMarketWeight = 0.7;
}
