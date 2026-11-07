namespace PossiblyMaybe.Domain.SportPacks;

/// <summary>
/// NHL through the sport-pack template. Shadow gates = <see cref="NhlShadowConfig"/>.
/// Tier A gated on O→C fill (tracker age / Odds API); until then shadow + line_tracker only.
/// Oregon: no NHL franchise — keep Winterhawks WHL names so alias bugs never map wrong.
/// </summary>
public static class NhlSportPack
{
    public static SportPack Instance { get; } = new(
        LeagueId: "NHL",
        ResultsSource: "ESPN hockey/nhl scoreboard (refresh-results)",
        OddsSources:
        [
            "line_tracker / OddsSnapshots (Kalshi KXNHLTOTAL + Crypto.com kind=NHL + ESPN open)",
            "Odds API / paid backfill (planned — unlocks Tier A)",
            "SBRO / archive.org (none found free multi-season O→C)",
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
        // shortcut: empty until O→C exists; do not invent folds
        TierAFolds: [],
        TrainMaxSeason: "none",
        TierCHoldoutSeason: "none",
        SkipSeason: "none",
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
