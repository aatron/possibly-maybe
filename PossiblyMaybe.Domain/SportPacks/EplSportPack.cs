namespace PossiblyMaybe.Domain.SportPacks;

/// <summary>
/// EPL through the sport-pack template. Gates = <see cref="EplChampConfig"/> (untuned; shadow live).
/// Holdout locked before tuning: train ≤2024-2025, Tier C 2025-2026.
/// </summary>
public static class EplSportPack
{
    public static SportPack Instance { get; } = new(
        LeagueId: "EPL",
        ResultsSource: "football-data / Kaggle Premier League scores (E0); ESPN scoreboard refresh (eng.1)",
        OddsSources:
        [
            "kaggle/football-odds-movements (Pinnacle O/C → premier_league/totals_open_close.csv)",
            "archive.org (planned)",
            "Kalshi KXEPLTOTAL / Crypto.com kind=EPL / line_tracker",
        ],
        RatingBookKind: "ScoringBook-goals-EMA",
        OregonNames: [], // no Oregon clubs in EPL; OregonExclusion still applied on every pick
        ResolveAlias: SportTeams.TryEpl,
        // shortcut: placeholders until A5 seasonality scan fills real windows
        SeasonPhases:
        [
            "opening-weeks",
            "intl-break",
            "final-weeks",
            "promoted-first-games",
        ],
        TierAFolds: ["2018-2019", "2019-2020", "2020-2021", "2021-2022", "2022-2023", "2023-2024", "2024-2025"],
        TrainMaxSeason: "2024-2025",
        TierCHoldoutSeason: "2025-2026",
        SkipSeason: "",
        Market: EplChampConfig.Market,
        Model: EplChampConfig.Model,
        MarketWeight: EplChampConfig.MarketWeight,
        MinEv: EplChampConfig.MinEv,
        MinAbsTotalEdge: EplChampConfig.MinAbsTotalEdge,
        EmaAlpha: EplChampConfig.EmaAlpha,
        BankrollSeed: EplChampConfig.BankrollSeed);

    public static SportPack Create() => Instance;
}
