namespace PossiblyMaybe.Domain;

/// <summary>
/// MLS totals paper champ (HOLD PASS 2025): w=0.40, |exp−line|≥0.25, EV≥4%.
/// xG EMA when available. EmaAlpha 0.206 (hl≈3) from library time-decay:
/// hold +15.7u/100 CLV+0.19 — beats prior α=0.18 (+12.2u) and Dixon–Coles (+13.2u).
/// Rollback: α=0.18 / git checkout 311127f -- PossiblyMaybe.Domain/MlsChampConfig.cs
/// </summary>
public static class MlsChampConfig
{
    public const string Market = "TOTAL";
    public const string Model = "MlsXgEma";
    public const double MarketWeight = 0.40;
    public const double MinEv = 0.04;
    public const double MinAbsTotalEdge = 0.25;
    public const double EmaAlpha = 0.206; // half-life ≈ 3 games
    public const double BankrollSeed = 1000;
}
