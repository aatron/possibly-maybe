namespace PossiblyMaybe.Domain;

/// <summary>
/// Liga MX totals paper candidate (library α sweep KEEP): w=0.40, |exp−line|≥0.25, EV≥4%.
/// Goals EMA α=0.129 (hl≈5). Hold 2024-2025: +3.5u / 52, CLV +0.24 (prior α=0.18 FAIL n&lt;40).
/// No live slate yet — paper config only.
/// </summary>
public static class LigaMxChampConfig
{
    public const string Market = "TOTAL";
    public const string Model = "LigaMxGoalsEma";
    public const double MarketWeight = 0.40;
    public const double MinEv = 0.04;
    public const double MinAbsTotalEdge = 0.25;
    public const double EmaAlpha = 0.129; // half-life ≈ 5 games
    public const double BankrollSeed = 1000;
}
