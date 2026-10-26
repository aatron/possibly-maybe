namespace PossiblyMaybe.Domain;

/// <summary>
/// Ligue 1 totals paper champ (HOLD PASS 2024-2025): w=0.40, |exp−line|≥0.25, EV≥4%.
/// Goals EMA only. Thin CLV — paper until trust month.
/// EmaAlpha 0.206 (hl≈3) from library time-decay: hold +10.1u/53 CLV+0.02
/// vs prior α=0.18 (+8.4u/49). Rollback: α=0.18.
/// </summary>
public static class Ligue1ChampConfig
{
    public const string Market = "TOTAL";
    public const string Model = "Ligue1GoalsEma";
    public const double MarketWeight = 0.40;
    public const double MinEv = 0.04;
    public const double MinAbsTotalEdge = 0.25;
    public const double EmaAlpha = 0.206; // half-life ≈ 3 games
    public const double BankrollSeed = 1000;
}
