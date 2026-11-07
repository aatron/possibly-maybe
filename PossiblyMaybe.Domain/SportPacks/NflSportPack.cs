namespace PossiblyMaybe.Domain.SportPacks;

/// <summary>
/// NFL through the sport-pack template. Research gates = <see cref="NflChampConfig"/> (HOLD FAIL).
/// Holdout locked: train &lt;2024, holdout 2024 (prior FAIL; not a fresh Tier C).
/// No Oregon NFL franchise — OregonNames empty by design (cardinal rule still applies via name list).
/// </summary>
public static class NflSportPack
{
    public static SportPack Instance { get; } = new(
        LeagueId: "NFL",
        ResultsSource: "nflverse games + NflGameContext (data/raw/nfl/nflverse_games.csv); ESPN refresh n/a",
        OddsSources:
        [
            "SBRO / flancast nfl_archive O→C (import-round2-odds, import-disk-odds)",
            "ESPN BET open/close (data/raw/espn_nfl_ou/)",
            "minmaxer nfl_odds_2026 (thin)",
            "line_tracker / OddsSnapshots (KXNFLTOTAL + shadow 1H/team series)",
        ],
        RatingBookKind: "EPA-pregame + ScoringBook-EMA-fallback",
        // No Oregon-based NFL team; keep explicit empty so pack OregonNamesExcluded() is vacuously true.
        OregonNames: [],
        ResolveAlias: SportTeams.TryNfl,
        // shortcut: placeholders until A5 seasonality scan fills real windows
        SeasonPhases:
        [
            "weeks-1-4",
            "midseason",
            "stretch-run",
            "playoffs",
        ],
        // Pre-declared folds with filled O→C in warehouse (2007–21 SBRO/flancast; ESPN 2023).
        TierAFolds:
        [
            "2015", "2016", "2017", "2018", "2019", "2020", "2021", "2023",
        ],
        TrainMaxSeason: "2023",
        TierCHoldoutSeason: "2024",
        SkipSeason: "2025",
        Market: NflChampConfig.Market,
        Model: NflChampConfig.Model,
        MarketWeight: NflChampConfig.MarketWeight,
        MinEv: NflChampConfig.MinEv,
        MinAbsTotalEdge: NflChampConfig.MinAbsTotalEdge,
        EmaAlpha: NflChampConfig.EmaAlpha,
        BankrollSeed: NflChampConfig.BankrollSeed);

    public static SportPack Create() => Instance;
}
