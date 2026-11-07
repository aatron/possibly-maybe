namespace PossiblyMaybe.Domain.SportPacks;

/// <summary>
/// MLS through the sport-pack template. Champ numbers = <see cref="MlsChampConfig"/> (V0 unchanged).
/// Holdout locked per MlsPack.md / NOTES_MLS: train ≤2024, holdout 2025, skip 2026.
/// </summary>
public static class MlsSportPack
{
    public static SportPack Instance { get; } = new(
        LeagueId: "MLS",
        ResultsSource: "ASA games + xG (data/raw/mls/asa_*.json); ESPN scoreboard refresh",
        OddsSources:
        [
            "kaggle/football-odds-movements (Pinnacle O/C → mls_totals_open_close.csv)",
            "archive.org (planned)",
            "SBRO (n/a for MLS)",
            "line_tracker / OddsSnapshots",
        ],
        RatingBookKind: "ScoringBook-xG-EMA", // model id MlsXgEma; goals EMA fallback
        OregonNames:
        [
            "Portland Timbers",
            "Portland Timbers FC",
            "timbers",
            "POR",
        ],
        ResolveAlias: SportTeams.TryMls,
        // Locked in research_notes/round4/a5_proof.md (A5); windows in SeasonalityPhases.
        SeasonPhases:
        [
            "opening-weeks",
            "summer-intl-break",
            "final-weeks",
            "playoffs",
        ],
        // shortcut: pre-registered fold labels; live Tier A still discovers seasons with odds
        TierAFolds: ["2019", "2020", "2021", "2022", "2023", "2024"],
        TrainMaxSeason: "2024",
        TierCHoldoutSeason: "2025",
        SkipSeason: "2026",
        Market: MlsChampConfig.Market,
        Model: MlsChampConfig.Model,
        MarketWeight: MlsChampConfig.MarketWeight,
        MinEv: MlsChampConfig.MinEv,
        MinAbsTotalEdge: MlsChampConfig.MinAbsTotalEdge,
        EmaAlpha: MlsChampConfig.EmaAlpha,
        BankrollSeed: MlsChampConfig.BankrollSeed);

    public static SportPack Create() => Instance;
}
