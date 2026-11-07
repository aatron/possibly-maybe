namespace PossiblyMaybe.Domain;

/// <summary>
/// NFL totals research gates — HOLD FAIL (hunt_nfl.md 2026-10-29: hold −1.6u / 57).
/// Not a money champ; pack + Tier A only. Do not wire into paper-go.
/// Pack form: <c>SportPacks.NflSportPack</c> (references these consts).
/// </summary>
public static class NflChampConfig
{
    public const string Market = "TOTAL";
    public const string Model = "NflEpaRoof"; // wind is a separate arm (nfl-wind)
    public const double MarketWeight = 0.50; // hunt train champ
    public const double MinEv = 0.04;
    public const double MinAbsTotalEdge = 2.0;
    public const double EmaAlpha = 0.15; // ScoringBook fallback when EPA missing
    public const double BankrollSeed = 1000;
}
