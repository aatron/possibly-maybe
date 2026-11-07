namespace PossiblyMaybe.Domain;

/// <summary>
/// MLB totals gates for C4 Tier A / look-once 2021. Locked before peeking 2021
/// (see research_notes/round4/c4_proof.md). N_trials=1 — no grid on selection.
/// Pack form: <c>SportPacks.MlbSportPack</c>.
/// </summary>
public static class MlbChampConfig
{
    public const string Market = "TOTAL";
    public const string Model = "MlbSpFipPark";
    public const double MarketWeight = 0.50;
    public const double MinEv = 0.04;
    public const double MinAbsTotalEdge = 1.0;
    public const double EmaAlpha = 0.12;
    public const double BankrollSeed = 1000;
}
