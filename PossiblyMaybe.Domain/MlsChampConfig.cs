namespace PossiblyMaybe.Domain;

/// <summary>
/// MLS totals paper champ (HOLD PASS 2025 DC): α=0.12, w=0.50, |exp−line|≥0.25, EV≥4%.
/// Dixon–Coles att-def EMA (xG when joinable). Beat prior xG-EMA +12.2u/93 CLV+0.18 → +13.2u/116 CLV+0.18.
/// Rollback: git checkout 311127f -- PossiblyMaybe.Domain/MlsChampConfig.cs PossiblyMaybe.Cli/Paper/DailySlate.cs
/// </summary>
public static class MlsChampConfig
{
    public const string Market = "TOTAL";
    public const string Model = "MlsDixonColesXg";
    public const double MarketWeight = 0.50;
    public const double MinEv = 0.04;
    public const double MinAbsTotalEdge = 0.25;
    public const double DcAlpha = 0.12;
    public const double BankrollSeed = 1000;
}
