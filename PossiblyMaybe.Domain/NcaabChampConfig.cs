namespace PossiblyMaybe.Domain;

/// <summary>
/// NCAAB totals provisional champ (C1): market blend + ScoringBook / PaceEff when boxes exist.
/// Gates are provisional — locked before Tier C peek; do not retune on 2021-22.
/// Pack form: <c>SportPacks.NcaabSportPack</c>.
/// </summary>
public static class NcaabChampConfig
{
    public const string Market = "TOTAL";
    public const string Model = "NcaabPaceEff";
    public const double MarketWeight = 0.50;
    public const double MinEv = 0.04;
    public const double MinAbsTotalEdge = 5.0;
    public const double EmaAlpha = 0.15;
    public const double LeagueInitTotal = 140; // typical D-I O/U level
    public const double BankrollSeed = 1000;
}
