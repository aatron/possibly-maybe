namespace PossiblyMaybe.Domain.SportPacks;

/// <summary>
/// La Liga through the sport-pack template. Shadow gates = <see cref="LaLigaShadowConfig"/> (FAIL champ).
/// Holdout locked: train ≤2023-2024, Tier C / burned holdout 2024-2025, skip 2025-2026.
/// </summary>
public static class LaLigaSportPack
{
    public static SportPack Instance { get; } = new(
        LeagueId: "LALIGA",
        ResultsSource: "Kaggle football-data scores; ESPN scoreboard refresh (esp.1)",
        OddsSources:
        [
            "kaggle/football-odds-movements (Pinnacle O/C)",
            "archive.org (planned)",
            "line_tracker / OddsSnapshots",
        ],
        RatingBookKind: "ScoringBook-goals-EMA",
        OregonNames: [], // no Oregon clubs in La Liga; OregonExclusion still applied on every pick
        ResolveAlias: SportTeams.TryLaLiga,
        // shortcut: placeholders until A5 seasonality scan fills real windows
        SeasonPhases:
        [
            "opening-weeks",
            "winter-break",
            "final-weeks",
            "promoted-first-games",
        ],
        TierAFolds: ["2018-2019", "2019-2020", "2020-2021", "2021-2022", "2022-2023", "2023-2024"],
        TrainMaxSeason: "2023-2024",
        TierCHoldoutSeason: "2024-2025",
        SkipSeason: "2025-2026",
        Market: LaLigaShadowConfig.Market,
        Model: LaLigaShadowConfig.Model,
        MarketWeight: LaLigaShadowConfig.MarketWeight,
        MinEv: LaLigaShadowConfig.MinEv,
        MinAbsTotalEdge: LaLigaShadowConfig.MinAbsTotalEdge,
        EmaAlpha: LaLigaShadowConfig.EmaAlpha,
        BankrollSeed: LaLigaShadowConfig.BankrollSeed);

    public static SportPack Create() => Instance;
}
