using PossiblyMaybe.Domain;
using PossiblyMaybe.Domain.SportPacks;

namespace PossiblyMaybe.Tests;

public class SportPackTests
{
    [Test]
    public async Task Mls_Pack_Matches_ChampConfig_And_Oregon()
    {
        var pack = SportPackRegistry.Get("MLS");

        await Assert.That(pack.LeagueId).IsEqualTo("MLS");
        await Assert.That(pack.Market).IsEqualTo(MlsChampConfig.Market);
        await Assert.That(pack.Model).IsEqualTo(MlsChampConfig.Model);
        await Assert.That(pack.MarketWeight).IsEqualTo(MlsChampConfig.MarketWeight);
        await Assert.That(pack.MinEv).IsEqualTo(MlsChampConfig.MinEv);
        await Assert.That(pack.MinAbsTotalEdge).IsEqualTo(MlsChampConfig.MinAbsTotalEdge);
        await Assert.That(pack.EmaAlpha).IsEqualTo(MlsChampConfig.EmaAlpha);
        await Assert.That(pack.BankrollSeed).IsEqualTo(MlsChampConfig.BankrollSeed);
        await Assert.That(pack.TrainMaxSeason).IsEqualTo("2024");
        await Assert.That(pack.TierCHoldoutSeason).IsEqualTo("2025");
        await Assert.That(pack.SkipSeason).IsEqualTo("2026");
        await Assert.That(pack.RatingBookKind).IsEqualTo("ScoringBook-xG-EMA");
        await Assert.That(pack.ResultsSource.Length).IsGreaterThan(0);
        await Assert.That(pack.OddsSources.Count).IsGreaterThanOrEqualTo(3);
        await Assert.That(pack.TierAFolds).IsEquivalentTo(["2019", "2020", "2021", "2022", "2023", "2024"]);
        await Assert.That(pack.SeasonPhases.Count).IsGreaterThan(0);
        await Assert.That(pack.ResolveAlias("Portland Timbers")).IsEqualTo("POR");
        await Assert.That(pack.ResolveAlias("Seattle Sounders")).IsEqualTo("SEA");
        await Assert.That(pack.OregonNamesExcluded()).IsTrue();

        // Cardinal rule: Timbers / Thorns / Fire name variants still excluded
        await Assert.That(OregonExclusion.IsExcluded("MLS", "Portland Timbers")).IsTrue();
        await Assert.That(OregonExclusion.IsExcluded("MLS", "Portland Timbers FC")).IsTrue();
        await Assert.That(OregonExclusion.IsExcluded("MLS", "timbers")).IsTrue();
        await Assert.That(OregonExclusion.IsExcluded("MLS", "POR")).IsTrue();
        await Assert.That(OregonExclusion.IsExcluded("NWSL", "Portland Thorns")).IsTrue();
        await Assert.That(OregonExclusion.IsExcluded("NWSL", "Portland Thorns FC")).IsTrue();
        await Assert.That(OregonExclusion.IsExcluded("NWSL", "thorns")).IsTrue();
        await Assert.That(OregonExclusion.IsExcluded("WNBA", "Portland Fire")).IsTrue();
        await Assert.That(OregonExclusion.IsExcluded("WNBA", "POR")).IsTrue();
        await Assert.That(OregonExclusion.IsExcluded("MLS", "Chicago Fire")).IsFalse();
    }

    [Test]
    public async Task LaLiga_And_Efl_Packs_Shadow_Gates_And_Oregon()
    {
        var la = SportPackRegistry.Get("LALIGA");
        await Assert.That(la.LeagueId).IsEqualTo("LALIGA");
        await Assert.That(la.Market).IsEqualTo(LaLigaShadowConfig.Market);
        await Assert.That(la.Model).IsEqualTo(LaLigaShadowConfig.Model);
        await Assert.That(la.MarketWeight).IsEqualTo(LaLigaShadowConfig.MarketWeight);
        await Assert.That(la.MinEv).IsEqualTo(LaLigaShadowConfig.MinEv);
        await Assert.That(la.MinAbsTotalEdge).IsEqualTo(LaLigaShadowConfig.MinAbsTotalEdge);
        await Assert.That(la.EmaAlpha).IsEqualTo(LaLigaShadowConfig.EmaAlpha);
        await Assert.That(la.TierCHoldoutSeason).IsEqualTo("2024-2025");
        await Assert.That(la.SkipSeason).IsEqualTo("2025-2026");
        await Assert.That(la.OregonNames.Count).IsEqualTo(0);
        await Assert.That(la.OregonNamesExcluded()).IsTrue();
        await Assert.That(la.ResolveAlias("Barcelona")).IsEqualTo("FC Barcelona");
        await Assert.That(la.ResolveAlias("Atlético Madrid")).IsEqualTo("Atletico Madrid");
        await Assert.That(SportTeams.ResolveSoccer("LALIGA", "BAR", "Barcelona", "Barcelona")).IsEqualTo("FC Barcelona");

        var efl = SportPackRegistry.Get("EFL");
        await Assert.That(efl.LeagueId).IsEqualTo("EFL");
        await Assert.That(efl.Model).IsEqualTo(EflShadowConfig.Model);
        await Assert.That(efl.MarketWeight).IsEqualTo(EflShadowConfig.MarketWeight);
        await Assert.That(efl.EmaAlpha).IsEqualTo(EflShadowConfig.EmaAlpha);
        await Assert.That(efl.OregonNamesExcluded()).IsTrue();
        await Assert.That(efl.ResolveAlias("QPR")).IsEqualTo("Queens Park Rangers (QPR)");
        await Assert.That(efl.ResolveAlias("West Brom")).IsEqualTo("West Bromwich(WBA)");
        await Assert.That(SportTeams.ResolveSoccer("EFL", "WBA", "West Bromwich Albion", "West Brom"))
            .IsEqualTo("West Bromwich(WBA)");

        // Cardinal rule still applies even though no Oregon clubs live in these leagues
        await Assert.That(OregonExclusion.IsExcluded("LALIGA", "Portland Timbers")).IsTrue();
        await Assert.That(OregonExclusion.IsExcluded("EFL", "Oregon Ducks")).IsTrue();
        await Assert.That(OregonExclusion.IsExcluded("LALIGA", "Real Madrid")).IsFalse();
        await Assert.That(OregonExclusion.IsExcluded("EFL", "Leeds United")).IsFalse();
    }

    [Test]

    public async Task Bundesliga_Pack_Locks_TierC_And_Oregon()
    {
        var pack = SportPackRegistry.Get("BUNDESLIGA");

        await Assert.That(pack.LeagueId).IsEqualTo("BUNDESLIGA");
        await Assert.That(pack.Market).IsEqualTo(BundesligaShadowConfig.Market);
        await Assert.That(pack.Model).IsEqualTo(BundesligaShadowConfig.Model);
        await Assert.That(pack.MarketWeight).IsEqualTo(BundesligaShadowConfig.MarketWeight);
        await Assert.That(pack.MinEv).IsEqualTo(BundesligaShadowConfig.MinEv);
        await Assert.That(pack.MinAbsTotalEdge).IsEqualTo(BundesligaShadowConfig.MinAbsTotalEdge);
        await Assert.That(pack.EmaAlpha).IsEqualTo(BundesligaShadowConfig.EmaAlpha);
        await Assert.That(pack.BankrollSeed).IsEqualTo(BundesligaShadowConfig.BankrollSeed);
        // Tier C locked in c6_bundesliga_proof.md before any gate tuning
        await Assert.That(pack.TrainMaxSeason).IsEqualTo("2024-2025");
        await Assert.That(pack.TierCHoldoutSeason).IsEqualTo("2025-2026");
        await Assert.That(pack.SkipSeason).IsEqualTo("");
        await Assert.That(pack.TierAFolds).IsEquivalentTo(
            ["2018-2019", "2019-2020", "2020-2021", "2021-2022", "2022-2023", "2023-2024"]);
        await Assert.That(pack.OregonNames.Count).IsEqualTo(0);
        await Assert.That(pack.OregonNamesExcluded()).IsTrue();
        await Assert.That(pack.ResolveAlias("Bayern Munich")).IsEqualTo("Bayern Munchen");
        await Assert.That(pack.ResolveAlias("Borussia Mönchengladbach")).IsEqualTo("Borussia Monchengladbach");
        await Assert.That(pack.ResolveAlias("FC Cologne")).IsEqualTo("FC Koln");
        await Assert.That(SportTeams.ResolveSoccer("BUNDESLIGA", "MUN", "Bayern Munich", "Bayern"))
            .IsEqualTo("Bayern Munchen");

        await Assert.That(OregonExclusion.IsExcluded("BUNDESLIGA", "Bayern Munich")).IsFalse();
        await Assert.That(OregonExclusion.IsExcluded("BUNDESLIGA", "Dortmund")).IsFalse();
        await Assert.That(OregonExclusion.IsExcluded("MLS", "Portland Timbers")).IsTrue();
    }

    [Test]
    public async Task Ncaab_Pack_Matches_ChampConfig_Oregon_And_HoldoutLock()
    {
        var pack = SportPackRegistry.Get("NCAAB");

        await Assert.That(pack.LeagueId).IsEqualTo("NCAAB");
        await Assert.That(pack.Market).IsEqualTo(NcaabChampConfig.Market);
        await Assert.That(pack.Model).IsEqualTo(NcaabChampConfig.Model);
        await Assert.That(pack.MarketWeight).IsEqualTo(NcaabChampConfig.MarketWeight);
        await Assert.That(pack.MinEv).IsEqualTo(NcaabChampConfig.MinEv);
        await Assert.That(pack.MinAbsTotalEdge).IsEqualTo(NcaabChampConfig.MinAbsTotalEdge);
        await Assert.That(pack.EmaAlpha).IsEqualTo(NcaabChampConfig.EmaAlpha);
        await Assert.That(pack.BankrollSeed).IsEqualTo(NcaabChampConfig.BankrollSeed);
        // Tier C locked before tuning (last SBRO season)
        await Assert.That(pack.TrainMaxSeason).IsEqualTo("2020-21");
        await Assert.That(pack.TierCHoldoutSeason).IsEqualTo("2021-22");
        await Assert.That(pack.SkipSeason).IsEqualTo("2022-23");
        await Assert.That(NcaabTotalsBacktestHoldout()).IsEqualTo("2021-22");
        await Assert.That(pack.RatingBookKind).IsEqualTo("PaceEfficiencyBook+ScoringBook-fallback");
        await Assert.That(pack.TierAFolds).IsEquivalentTo(
            ["2015-16", "2016-17", "2017-18", "2018-19", "2019-20", "2020-21"]);
        await Assert.That(pack.ResolveAlias("Oregon Ducks")).IsEqualTo("ore");
        await Assert.That(pack.ResolveAlias("OregonState")).IsEqualTo("orst");
        await Assert.That(pack.ResolveAlias("Portland Pilots")).IsEqualTo("up");
        await Assert.That(pack.ResolveAlias("Duke")).IsEqualTo("duke");
        await Assert.That(pack.OregonNamesExcluded()).IsTrue();

        await Assert.That(OregonExclusion.IsExcluded("NCAAB", "Oregon Ducks")).IsTrue();
        await Assert.That(OregonExclusion.IsExcluded("NCAAB", "Oregon State")).IsTrue();
        await Assert.That(OregonExclusion.IsExcluded("NCAAB", "Portland State")).IsTrue();
        await Assert.That(OregonExclusion.IsExcluded("NCAAB", "Portland Pilots")).IsTrue();
        await Assert.That(OregonExclusion.IsExcluded("NCAAB", "Duke")).IsFalse();
    }

    // Avoid Cli project ref from tests for holdout string — pack is source of truth.
    static string NcaabTotalsBacktestHoldout() => SportPackRegistry.Ncaab.TierCHoldoutSeason;
    [Test]
    public async Task Epl_Pack_Holdout_Locked_Before_Tuning()
    {
        var pack = SportPackRegistry.Get("EPL");

        await Assert.That(pack.LeagueId).IsEqualTo("EPL");
        await Assert.That(pack.Market).IsEqualTo(EplChampConfig.Market);
        await Assert.That(pack.Model).IsEqualTo(EplChampConfig.Model);
        await Assert.That(pack.MarketWeight).IsEqualTo(EplChampConfig.MarketWeight);
        await Assert.That(pack.MinEv).IsEqualTo(EplChampConfig.MinEv);
        await Assert.That(pack.MinAbsTotalEdge).IsEqualTo(EplChampConfig.MinAbsTotalEdge);
        await Assert.That(pack.EmaAlpha).IsEqualTo(EplChampConfig.EmaAlpha);
        await Assert.That(pack.BankrollSeed).IsEqualTo(EplChampConfig.BankrollSeed);
        // Tier C locked in proof before any gate tuning
        await Assert.That(pack.TrainMaxSeason).IsEqualTo("2024-2025");
        await Assert.That(pack.TierCHoldoutSeason).IsEqualTo("2025-2026");
        await Assert.That(pack.RatingBookKind).IsEqualTo("ScoringBook-goals-EMA");
        await Assert.That(pack.OregonNames.Count).IsEqualTo(0);
        await Assert.That(pack.OregonNamesExcluded()).IsTrue();
        await Assert.That(pack.ResolveAlias("Brighton")).IsEqualTo("Brighton Hove Albion");
        await Assert.That(pack.ResolveAlias("Spurs")).IsEqualTo("Tottenham Hotspur");
        await Assert.That(pack.ResolveAlias("Wolverhampton Wanderers")).IsEqualTo("Wolves");
        await Assert.That(SportTeams.ResolveSoccer("EPL", "ARS", "Arsenal", "Arsenal")).IsEqualTo("Arsenal");

        // Cardinal rule still fires under EPL even with no Oregon clubs in the league
        await Assert.That(OregonExclusion.IsExcluded("EPL", "Portland Timbers")).IsTrue();
        await Assert.That(OregonExclusion.IsExcluded("EPL", "Oregon Ducks")).IsTrue();
        await Assert.That(OregonExclusion.IsExcluded("EPL", "Arsenal")).IsFalse();
        await Assert.That(OregonExclusion.IsExcluded("EPL", "Liverpool")).IsFalse();
    }


    [Test]
    public async Task Nfl_Pack_Matches_ChampConfig_Empty_Oregon()
    {
        var pack = SportPackRegistry.Get("NFL");

        await Assert.That(pack.LeagueId).IsEqualTo("NFL");
        await Assert.That(pack.Market).IsEqualTo(NflChampConfig.Market);
        await Assert.That(pack.Model).IsEqualTo(NflChampConfig.Model);
        await Assert.That(pack.MarketWeight).IsEqualTo(NflChampConfig.MarketWeight);
        await Assert.That(pack.MinEv).IsEqualTo(NflChampConfig.MinEv);
        await Assert.That(pack.MinAbsTotalEdge).IsEqualTo(NflChampConfig.MinAbsTotalEdge);
        await Assert.That(pack.EmaAlpha).IsEqualTo(NflChampConfig.EmaAlpha);
        await Assert.That(pack.BankrollSeed).IsEqualTo(NflChampConfig.BankrollSeed);
        await Assert.That(pack.TrainMaxSeason).IsEqualTo("2023");
        await Assert.That(pack.TierCHoldoutSeason).IsEqualTo("2024");
        await Assert.That(pack.SkipSeason).IsEqualTo("2025");
        await Assert.That(pack.OregonNames).IsEmpty();
        await Assert.That(pack.OregonNamesExcluded()).IsTrue();
        await Assert.That(pack.ResolveAlias("Green Bay Packers")).IsEqualTo("GB");
        await Assert.That(pack.OddsSources.Count).IsGreaterThanOrEqualTo(3);
        await Assert.That(pack.TierAFolds).IsEquivalentTo(
            ["2015", "2016", "2017", "2018", "2019", "2020", "2021", "2023"]);
        await Assert.That(NflShadowMarkets.ShadowTrackerSeries).IsEquivalentTo(
            ["KXNFLTOTAL", "KXNFL1HTOTAL", "KXNFLTEAMTOTAL"]);
        await Assert.That(NflWind.ArmId).IsEqualTo("nfl-wind");
        await Assert.That(NflWind.AdjustExpectedTotal(45, 20)).IsEqualTo(42);
        await Assert.That(NflWind.AdjustExpectedTotal(45, 10)).IsEqualTo(45);
        await Assert.That(NflWind.AdjustExpectedTotal(45, null)).IsEqualTo(45);
    }


    [Test]
    public async Task Mlb_Pack_Locks_Holdout_And_Spring_Ready()
    {
        var pack = SportPackRegistry.Get("MLB");

        await Assert.That(pack.LeagueId).IsEqualTo("MLB");
        await Assert.That(pack.Market).IsEqualTo(MlbChampConfig.Market);
        await Assert.That(pack.Model).IsEqualTo(MlbChampConfig.Model);
        await Assert.That(pack.MarketWeight).IsEqualTo(MlbChampConfig.MarketWeight);
        await Assert.That(pack.MinEv).IsEqualTo(MlbChampConfig.MinEv);
        await Assert.That(pack.MinAbsTotalEdge).IsEqualTo(MlbChampConfig.MinAbsTotalEdge);
        await Assert.That(pack.EmaAlpha).IsEqualTo(MlbChampConfig.EmaAlpha);
        await Assert.That(pack.BankrollSeed).IsEqualTo(MlbChampConfig.BankrollSeed);
        await Assert.That(pack.TrainMaxSeason).IsEqualTo("2019");
        await Assert.That(pack.TierCHoldoutSeason).IsEqualTo("2021");
        await Assert.That(pack.SkipSeason).IsEqualTo("2020");
        await Assert.That(pack.TierAFolds).IsEquivalentTo(
            ["2011", "2012", "2013", "2014", "2015", "2016", "2017", "2018", "2019"]);
        await Assert.That(pack.ResolveAlias("Yankees")).IsEqualTo("NYY");
        await Assert.That(pack.ResolveAlias("Dodgers")).IsEqualTo("LAD");
        await Assert.That(pack.OregonNamesExcluded()).IsTrue();
        await Assert.That(OregonExclusion.IsExcluded("MLB", "Hillsboro Hops")).IsTrue();
        await Assert.That(OregonExclusion.IsExcluded("MLB", "Eugene Emeralds")).IsTrue();

        await Assert.That(MlbSportPack.SpringSubMarketsReady).IsTrue();
        await Assert.That(MlbSportPack.F5ModelReady).IsFalse();
        await Assert.That(MlbSportPack.TeamTotalsModelReady).IsFalse();
        // Market / series labels are consts — length checks keep them non-empty without TUnit const assert
        await Assert.That(MlbSportPack.F5Market.Length).IsGreaterThan(0);
        await Assert.That(MlbSportPack.TeamTotalMarket.Length).IsGreaterThan(0);
        await Assert.That(MlbSportPack.F5KalshiSeries.StartsWith("KXMLB")).IsTrue();
        await Assert.That(MlbSportPack.TeamTotalKalshiSeries.StartsWith("KXMLB")).IsTrue();
        await Assert.That(SportPackRegistry.Mlb.TierCHoldoutSeason).IsEqualTo("2021");
    }


    [Test]
    public async Task Ncaaf_Pack_Locks_TierC_And_Oregon()
    {
        var pack = SportPackRegistry.Get("NCAAF");

        await Assert.That(pack.LeagueId).IsEqualTo("NCAAF");
        await Assert.That(pack.Market).IsEqualTo(NcaafChampConfig.Market);
        await Assert.That(pack.Model).IsEqualTo(NcaafChampConfig.Model);
        await Assert.That(pack.MarketWeight).IsEqualTo(NcaafChampConfig.MarketWeight);
        await Assert.That(pack.MinEv).IsEqualTo(NcaafChampConfig.MinEv);
        await Assert.That(pack.MinAbsTotalEdge).IsEqualTo(NcaafChampConfig.MinAbsTotalEdge);
        await Assert.That(pack.EmaAlpha).IsEqualTo(NcaafChampConfig.EmaAlpha);
        await Assert.That(pack.BankrollSeed).IsEqualTo(NcaafChampConfig.BankrollSeed);
        // Locked in c2_proof.md before any train sweep / holdout look
        await Assert.That(pack.TrainMaxSeason).IsEqualTo("2021-22");
        await Assert.That(pack.TierCHoldoutSeason).IsEqualTo("2022-23");
        await Assert.That(pack.SkipSeason).IsEqualTo("2023-24");
        await Assert.That(pack.RatingBookKind).IsEqualTo("ScoringBook-EMA");
        await Assert.That(pack.OddsSources.Any(s => s.Contains("SBRO", StringComparison.Ordinal))).IsTrue();
        await Assert.That(pack.TierAFolds.Contains("2021-22")).IsTrue();
        await Assert.That(pack.TierAFolds.Contains("2022-23")).IsFalse();
        await Assert.That(pack.ResolveAlias("OregonState")).IsEqualTo("orst");
        await Assert.That(pack.ResolveAlias("Oregon")).IsEqualTo("ore");
        await Assert.That(pack.OregonNamesExcluded()).IsTrue();
        await Assert.That(OregonExclusion.IsExcluded("NCAAF", "OregonState")).IsTrue();
        await Assert.That(OregonExclusion.IsExcluded("NCAAF", "Oregon")).IsTrue();
        await Assert.That(OregonExclusion.IsExcluded("NCAAF", "Ohio State")).IsFalse();
    }
}
