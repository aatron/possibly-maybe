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
}
