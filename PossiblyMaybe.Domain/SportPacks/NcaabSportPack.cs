namespace PossiblyMaybe.Domain.SportPacks;

/// <summary>
/// NCAAB totals through the sport-pack template (C1 / r3 W10).
/// Holdout locked before tuning: train ≤2020-21, Tier C = 2021-22 (last SBRO season), skip 2022-23+.
/// </summary>
public static class NcaabSportPack
{
    public static SportPack Instance { get; } = new(
        LeagueId: "NCAAB",
        ResultsSource: "SBRO xlsx scores (data/raw/round3_odds/sbro_ncaab/); ESPN mens-college-basketball refresh (live)",
        OddsSources:
        [
            "SBRO Wayback 2007-08→2021-22 (Open+Close totals, AssumedJuice −110)",
            "archive.org (same SBRO snapshots)",
            "line_tracker / OddsSnapshots (Kalshi KXNCAAMBTOTAL when wired)",
            "CBBD / Odds API (planned — post-2022 O→C)",
        ],
        // shortcut: true PaceEff needs GameBoxes; ScoringBook fallback until ESPN box import
        RatingBookKind: "PaceEfficiencyBook+ScoringBook-fallback",
        OregonNames:
        [
            "Oregon", "Oregon Ducks", "Ducks", "ore",
            "Oregon State", "OregonState", "Oregon St", "Oregon St.", "Oregon State Beavers", "Beavers", "orst",
            "Portland State", "PortlandState", "Portland St", "Portland St.", "Portland State Vikings", "port",
            "Portland U", "PortlandU", "University of Portland", "Portland Pilots", "Pilots", "up", "portlandu",
        ],
        ResolveAlias: raw => string.IsNullOrWhiteSpace(raw) ? null : SportTeams.NcaaCode(raw),
        // shortcut: placeholders until A5 seasonality scan
        SeasonPhases:
        [
            "opening-weeks",
            "conference-play",
            "conference-tournaments",
            "ncaa-tournament",
        ],
        TierAFolds: ["2015-16", "2016-17", "2017-18", "2018-19", "2019-20", "2020-21"],
        TrainMaxSeason: "2020-21",
        TierCHoldoutSeason: "2021-22",
        SkipSeason: "2022-23",
        Market: NcaabChampConfig.Market,
        Model: NcaabChampConfig.Model,
        MarketWeight: NcaabChampConfig.MarketWeight,
        MinEv: NcaabChampConfig.MinEv,
        MinAbsTotalEdge: NcaabChampConfig.MinAbsTotalEdge,
        EmaAlpha: NcaabChampConfig.EmaAlpha,
        BankrollSeed: NcaabChampConfig.BankrollSeed);

    public static SportPack Create() => Instance;
}
