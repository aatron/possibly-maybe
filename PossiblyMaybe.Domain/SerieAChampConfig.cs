namespace PossiblyMaybe.Domain;

/// <summary>
/// Serie A totals paper champ (HOLD PASS 2024-2025 DC): α=0.18, w=0.40, |exp−line|≥0.35, EV≥4%.
/// Dixon–Coles att-def EMA (goals). Prior goals-EMA FAIL (−8.5u/40) → DC PASS +1.7u/113 CLV+0.03.
/// Rollback: delete this file / git checkout 311127f -- PossiblyMaybe.Domain/SerieAChampConfig.cs
/// </summary>
public static class SerieAChampConfig
{
    public const string Market = "TOTAL";
    public const string Model = "SerieADixonColes";
    public const double MarketWeight = 0.40;
    public const double MinEv = 0.04;
    public const double MinAbsTotalEdge = 0.35;
    public const double DcAlpha = 0.18;
    public const double BankrollSeed = 1000;
}
