namespace PossiblyMaybe.Domain.SportPacks;

/// <summary>
/// NHL through the sport-pack template. Shadow gates = <see cref="NhlShadowConfig"/>.
/// T6: Tier A unlocked after T2a SBRO O→C (≥80%). Newest full season = look-once Tier C.
/// Oregon: no NHL franchise — keep Winterhawks WHL names so alias bugs never map wrong.
/// </summary>
public static class NhlSportPack
{
    public static SportPack Instance { get; } = new(
        LeagueId: "NHL",
        ResultsSource: "ESPN hockey/nhl scoreboard (refresh-results)",
        OddsSources:
        [
            "SBRO Wayback OpenOU/CloseOU (import-nhl-sbro; T2a — unlocks Tier A)",
            "line_tracker / OddsSnapshots (Kalshi KXNHLTOTAL + Crypto.com kind=NHL + ESPN open)",
        ],
        RatingBookKind: "ScoringBook-goals-EMA",
        OregonNames:
        [
            "Portland Winterhawks",
            "portland winterhawks",
            "Winterhawks",
            "winterhawks",
        ],
        ResolveAlias: SportTeams.TryNhl,
        // shortcut: placeholders until A5 seasonality scan
        SeasonPhases:
        [
            "opening-weeks",
            "mid-season",
            "trade-deadline",
            "playoffs",
        ],
        // Pre-holdout folds with filled O→C (T2a). Newest full season = Tier C.
        TierAFolds:
        [
            "2015-2016", "2016-2017", "2017-2018", "2018-2019", "2019-2020", "2020-2021",
        ],
        TrainMaxSeason: "2020-2021",
        TierCHoldoutSeason: "2021-2022",
        SkipSeason: "2022-2023",
        Market: NhlShadowConfig.Market,
        Model: NhlShadowConfig.Model,
        MarketWeight: NhlShadowConfig.MarketWeight,
        MinEv: NhlShadowConfig.MinEv,
        MinAbsTotalEdge: NhlShadowConfig.MinAbsTotalEdge,
        EmaAlpha: NhlShadowConfig.EmaAlpha,
        BankrollSeed: NhlShadowConfig.BankrollSeed);

    public static SportPack Create() => Instance;

    /// <summary>True when Tier A walk-forward is allowed (needs historical open→close).</summary>
    public static bool TierAReady => Instance.TierAFolds.Count > 0;
}
