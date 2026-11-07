namespace PossiblyMaybe.Domain.SportPacks;

/// <summary>
/// MLB through the sport-pack template (C4). Champ numbers = <see cref="MlbChampConfig"/>.
/// Holdout locked in c4_proof.md before tuning: Tier A 2011–2019, look-once 2021, skip 2020.
/// </summary>
public static class MlbSportPack
{
    public static SportPack Instance { get; } = new(
        LeagueId: "MLB",
        ResultsSource: "flancast mlb_archive_10Y + Arnav/Kaggle closing odds → Games+OddsSnapshots; ESPN scoreboard (live)",
        OddsSources:
        [
            "github flancast90 mlb_archive_10Y (2011–2021 O/C)",
            "kaggle/arnav MLB closing odds (2021–2025)",
            "archive.org (planned)",
            "line_tracker / OddsSnapshots (KXMLBTOTAL)",
        ],
        RatingBookKind: "SP-FIP × park (ScoringBook-runs-EMA fallback)",
        OregonNames:
        [
            "Hillsboro Hops",
            "Eugene Emeralds",
            "Portland Pickles",
        ],
        ResolveAlias: SportTeams.TryMlb,
        // shortcut: placeholders until A5 seasonality scan fills real windows
        SeasonPhases:
        [
            "opening-weeks",
            "summer",
            "september-stretch",
            "playoffs",
        ],
        TierAFolds: ["2011", "2012", "2013", "2014", "2015", "2016", "2017", "2018", "2019"],
        TrainMaxSeason: "2019",
        TierCHoldoutSeason: "2021",
        SkipSeason: "2020",
        Market: MlbChampConfig.Market,
        Model: MlbChampConfig.Model,
        MarketWeight: MlbChampConfig.MarketWeight,
        MinEv: MlbChampConfig.MinEv,
        MinAbsTotalEdge: MlbChampConfig.MinAbsTotalEdge,
        EmaAlpha: MlbChampConfig.EmaAlpha,
        BankrollSeed: MlbChampConfig.BankrollSeed);

    public static SportPack Create() => Instance;

    /// <summary>Pack declares F5 + team-total markets for spring shadow; models not fitted.</summary>
    public static bool SpringSubMarketsReady => true;

    public const string F5Market = "F5_TOTAL";
    public const string TeamTotalMarket = "TEAM_TOTAL";
    public const string F5KalshiSeries = "KXMLBF5TOTAL";
    public const string TeamTotalKalshiSeries = "KXMLBTEAMTOTAL";

    // shortcut: no F5/team-total model yet — flip when spring features land
    public static bool F5ModelReady => false;
    public static bool TeamTotalsModelReady => false;
}
