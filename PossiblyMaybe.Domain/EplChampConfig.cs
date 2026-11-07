namespace PossiblyMaybe.Domain;

/// <summary>
/// EPL totals — shadow until Tier A PASS. Untuned goals EMA α=0.18, w=0.40,
/// |exp−line|≥0.25, EV≥4% (FD soccer starting band). Tier C holdout locked in
/// <see cref="SportPacks.EplSportPack"/> / c6_epl_proof.md before any gate tuning.
/// </summary>
public static class EplChampConfig
{
    public const string Market = "TOTAL";
    public const string Model = "EplGoalsEma";
    public const double MarketWeight = 0.40;
    public const double MinEv = 0.04;
    public const double MinAbsTotalEdge = 0.25;
    public const double EmaAlpha = 0.18;
    public const double BankrollSeed = 1000;
}
