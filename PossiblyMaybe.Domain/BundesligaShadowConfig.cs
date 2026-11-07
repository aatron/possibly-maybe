namespace PossiblyMaybe.Domain;

/// <summary>
/// Bundesliga totals — shadow only until Tier A/C pass under the locked holdout.
/// Goals EMA α=0.18, same frozen gates as other unproven soccer packs
/// (w=0.40, |exp−line|≥0.25, EV≥4%). Do not tune against Tier C 2025-2026.
/// </summary>
public static class BundesligaShadowConfig
{
    public const string Market = "TOTAL";
    public const string Model = "BundesligaGoalsEma";
    public const double MarketWeight = 0.40;
    public const double MinEv = 0.04;
    public const double MinAbsTotalEdge = 0.25;
    public const double EmaAlpha = 0.18;
    public const double BankrollSeed = 1000;
}
