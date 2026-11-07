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
    public async Task Nhl_Pack_ShadowOnly_And_WinterhawksOregon()
    {
        var pack = SportPackRegistry.Get("NHL");

        await Assert.That(pack.LeagueId).IsEqualTo("NHL");
        await Assert.That(pack.Market).IsEqualTo(NhlShadowConfig.Market);
        await Assert.That(pack.Model).IsEqualTo(NhlShadowConfig.Model);
        await Assert.That(pack.MarketWeight).IsEqualTo(NhlShadowConfig.MarketWeight);
        await Assert.That(pack.MinEv).IsEqualTo(NhlShadowConfig.MinEv);
        await Assert.That(pack.MinAbsTotalEdge).IsEqualTo(NhlShadowConfig.MinAbsTotalEdge);
        await Assert.That(pack.EmaAlpha).IsEqualTo(NhlShadowConfig.EmaAlpha);
        await Assert.That(pack.BankrollSeed).IsEqualTo(NhlShadowConfig.BankrollSeed);
        await Assert.That(pack.RatingBookKind).IsEqualTo("ScoringBook-goals-EMA");
        await Assert.That(pack.ResultsSource).Contains("hockey/nhl");
        await Assert.That(pack.OddsSources.Any(s => s.Contains("KXNHLTOTAL"))).IsTrue();
        await Assert.That(pack.TierAFolds.Count).IsEqualTo(0);
        await Assert.That(NhlSportPack.TierAReady).IsFalse();
        await Assert.That(pack.TrainMaxSeason).IsEqualTo("none");
        await Assert.That(pack.SeasonPhases.Count).IsGreaterThan(0);
        await Assert.That(pack.ResolveAlias("Boston Bruins")).IsEqualTo("BOS");
        await Assert.That(pack.ResolveAlias("VGK")).IsEqualTo("VGK");
        await Assert.That(pack.OregonNamesExcluded()).IsTrue();

        // Cardinal: Winterhawks (WHL) stay excluded under NHL sport id too
        await Assert.That(OregonExclusion.IsExcluded("NHL", "Portland Winterhawks")).IsTrue();
        await Assert.That(OregonExclusion.IsExcluded("NHL", "winterhawks")).IsTrue();
        await Assert.That(OregonExclusion.IsExcluded("NHL", "BOS")).IsFalse();
        await Assert.That(OregonExclusion.IsExcluded("NHL", "Seattle Kraken")).IsFalse();
    }
}
