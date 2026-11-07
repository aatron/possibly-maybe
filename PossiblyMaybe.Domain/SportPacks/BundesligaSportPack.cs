namespace PossiblyMaybe.Domain.SportPacks;

/// <summary>
/// Bundesliga through the sport-pack template. Shadow gates = <see cref="BundesligaShadowConfig"/>.
/// Holdout locked in c6_bundesliga_proof.md before tuning: train ≤2024-2025, Tier C 2025-2026.
/// </summary>
public static class BundesligaSportPack
{
    public static SportPack Instance { get; } = new(
        LeagueId: "BUNDESLIGA",
        ResultsSource: "football-data.co.uk D1.csv; ESPN scoreboard refresh (ger.1)",
        OddsSources:
        [
            "football-data.co.uk D1 (OU 2.5 open/close odds cols)",
            "kaggle/football-odds-movements (Pinnacle O/C → soccer/bundesliga/)",
            "line_tracker / OddsSnapshots (Kalshi KXBUNDESLIGATOTAL; Crypto kind TBD)",
        ],
        RatingBookKind: "ScoringBook-goals-EMA",
        OregonNames: [], // no Oregon clubs in Bundesliga; OregonExclusion still applied on every pick
        ResolveAlias: SportTeams.TryBundesliga,
        // shortcut: placeholders until A5 seasonality scan fills real windows
        SeasonPhases:
        [
            "opening-weeks",
            "winter-break",
            "final-weeks",
            "promoted-first-games",
        ],
        TierAFolds: ["2018-2019", "2019-2020", "2020-2021", "2021-2022", "2022-2023", "2023-2024"],
        TrainMaxSeason: "2024-2025",
        TierCHoldoutSeason: "2025-2026",
        SkipSeason: "",
        Market: BundesligaShadowConfig.Market,
        Model: BundesligaShadowConfig.Model,
        MarketWeight: BundesligaShadowConfig.MarketWeight,
        MinEv: BundesligaShadowConfig.MinEv,
        MinAbsTotalEdge: BundesligaShadowConfig.MinAbsTotalEdge,
        EmaAlpha: BundesligaShadowConfig.EmaAlpha,
        BankrollSeed: BundesligaShadowConfig.BankrollSeed);

    public static SportPack Create() => Instance;
}
