namespace PossiblyMaybe.Domain.SportPacks;

/// <summary>
/// EFL Championship through the sport-pack template. Shadow gates = <see cref="EflShadowConfig"/> (FAIL champ).
/// Holdout locked: train ≤2023-2024, Tier C / burned holdout 2024-2025, skip 2025-2026.
/// </summary>
public static class EflSportPack
{
    public static SportPack Instance { get; } = new(
        LeagueId: "EFL",
        ResultsSource: "Kaggle football-data Championship scores; ESPN scoreboard refresh (eng.2)",
        OddsSources:
        [
            "kaggle/football-odds-movements (Pinnacle O/C)",
            "archive.org (planned)",
            "line_tracker / OddsSnapshots",
        ],
        RatingBookKind: "ScoringBook-goals-EMA",
        OregonNames: [], // no Oregon clubs in EFL; OregonExclusion still applied on every pick
        ResolveAlias: SportTeams.TryEfl,
        // shortcut: placeholders until A5 seasonality scan fills real windows
        SeasonPhases:
        [
            "opening-weeks",
            "intl-break",
            "final-weeks",
            "promoted-first-games",
        ],
        TierAFolds: ["2018-2019", "2019-2020", "2020-2021", "2021-2022", "2022-2023", "2023-2024"],
        TrainMaxSeason: "2023-2024",
        TierCHoldoutSeason: "2024-2025",
        SkipSeason: "2025-2026",
        Market: EflShadowConfig.Market,
        Model: EflShadowConfig.Model,
        MarketWeight: EflShadowConfig.MarketWeight,
        MinEv: EflShadowConfig.MinEv,
        MinAbsTotalEdge: EflShadowConfig.MinAbsTotalEdge,
        EmaAlpha: EflShadowConfig.EmaAlpha,
        BankrollSeed: EflShadowConfig.BankrollSeed);

    public static SportPack Create() => Instance;
}
