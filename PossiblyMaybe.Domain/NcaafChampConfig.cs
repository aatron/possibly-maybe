namespace PossiblyMaybe.Domain;

/// <summary>
/// NCAAF totals gates (C2). Scoring EMA blended with open; assumed −110.
/// Tier C holdout locked to 2022-23 in <c>NcaafSportPack</c> / c2_proof.md before train sweep.
/// Train-max selection: w=0.60, |edge|≥3 (holdout look-once only — see c2_proof.md).
/// Shadow candidate; paper promotion still needs wave radar + A1.
/// </summary>
public static class NcaafChampConfig
{
    public const string Market = "TOTAL";
    public const string Model = "NcaafScoringEma";
    public const double MarketWeight = 0.60;
    public const double MinEv = 0.04;
    public const double MinAbsTotalEdge = 3.0;
    public const double EmaAlpha = 0.15;
    public const double LeagueInit = 55; // typical FBS O/U mid-50s
    public const double BankrollSeed = 1000;
}
