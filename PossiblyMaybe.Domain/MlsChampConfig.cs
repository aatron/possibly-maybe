namespace PossiblyMaybe.Domain;

/// <summary>
/// MLS totals paper champ (HOLD PASS 2025): w=0.40, |exp−line|≥0.25, EV≥4%.
/// xG EMA when available. Thin edge bar — retail juice may eat live edge.
/// </summary>
public static class MlsChampConfig
{
    public const string Market = "TOTAL";
    public const string Model = "MlsXgEma";
    public const double MarketWeight = 0.40;
    public const double MinEv = 0.04;
    public const double MinAbsTotalEdge = 0.25;
    public const double BankrollSeed = 1000;
}
