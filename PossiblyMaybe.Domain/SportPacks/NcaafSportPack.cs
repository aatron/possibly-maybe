namespace PossiblyMaybe.Domain.SportPacks;

/// <summary>
/// NCAAF totals through the C0 sport-pack template. SBRO 2007–22 O→C (r3 W15).
/// Tier C holdout locked to <c>2022-23</c> before any gate sweep (see c2_proof.md).
/// Shadow continues through bowls even when SBRO snapshots miss late December.
/// </summary>
public static class NcaafSportPack
{
    public static SportPack Instance { get; } = new(
        LeagueId: "NCAAF",
        ResultsSource: "SBRO ncaa_football_*.xlsx scores (data/raw/round3_odds/sbro_ncaaf); CFBD/ESPN refresh planned",
        OddsSources:
        [
            "SBRO Wayback NCAAF 2007–2022 (round3_odds/sbro_ncaaf)",
            "CFBD /lines (planned, keyed)",
            "line_tracker / OddsSnapshots (KX NCAAF totals)",
        ],
        RatingBookKind: "ScoringBook-EMA", // model id NcaafScoringEma
        OregonNames:
        [
            "Oregon",
            "Oregon Ducks",
            "OregonState",
            "Oregon State",
            "Oregon St",
            "Oregon St.",
            "ORST",
            "ORE",
            "Beavers",
            "PortlandSt",
            "Portland State",
        ],
        ResolveAlias: SportTeams.NcaaCode,
        // shortcut: placeholders until A5 seasonality scan fills real windows
        SeasonPhases:
        [
            "weeks-1-4",
            "midseason",
            "conference-championships",
            "bowls",
        ],
        TierAFolds:
        [
            "2007-08", "2008-09", "2009-10", "2010-11", "2011-12", "2012-13",
            "2013-14", "2014-15", "2015-16", "2016-17", "2017-18", "2018-19",
            "2019-20", "2020-21", "2021-22",
        ],
        TrainMaxSeason: "2021-22",
        TierCHoldoutSeason: "2022-23",
        SkipSeason: "2023-24",
        Market: NcaafChampConfig.Market,
        Model: NcaafChampConfig.Model,
        MarketWeight: NcaafChampConfig.MarketWeight,
        MinEv: NcaafChampConfig.MinEv,
        MinAbsTotalEdge: NcaafChampConfig.MinAbsTotalEdge,
        EmaAlpha: NcaafChampConfig.EmaAlpha,
        BankrollSeed: NcaafChampConfig.BankrollSeed);

    public static SportPack Create() => Instance;
}
