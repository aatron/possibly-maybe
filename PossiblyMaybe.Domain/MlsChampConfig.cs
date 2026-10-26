namespace PossiblyMaybe.Domain;

/// <summary>
/// MLS totals paper champ (HOLD PASS 2025): w=0.40, |exp−line|≥0.25, EV≥4%.
/// xG EMA when available. Thin edge bar — retail juice may eat live edge.
/// EmaAlpha 0.206 (hl≈3 games) from library time-decay sweep: hold +15.7u/100 CLV+0.19
/// vs prior α=0.18 (+12.2u/93). Rollback: α=0.18.
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
