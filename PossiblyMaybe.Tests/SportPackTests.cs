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
