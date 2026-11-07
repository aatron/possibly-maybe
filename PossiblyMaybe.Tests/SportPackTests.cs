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
}
