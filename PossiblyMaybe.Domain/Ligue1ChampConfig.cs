namespace PossiblyMaybe.Domain;

/// <summary>
/// Ligue 1 totals paper champ (HOLD PASS 2024-2025): w=0.40, |exp−line|≥0.25, EV≥4%.
/// Goals EMA only. Thin CLV (+0.03) — paper until trust month.
/// </summary>
public static class Ligue1ChampConfig
{
    public const string Market = "TOTAL";
    public const string Model = "Ligue1GoalsEma";
    public const double MarketWeight = 0.40;
    public const double MinEv = 0.04;
    public const double MinAbsTotalEdge = 0.25;
    public const double BankrollSeed = 1000;
}
