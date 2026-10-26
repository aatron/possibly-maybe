using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Tests;

public class StakeSchemesTests
{
    [Test]
    public async Task All_HasSixNamedLanes()
    {
        await Assert.That(StakeSchemes.All.Count).IsEqualTo(6);
        await Assert.That(StakeSchemes.All.Select(s => s.Code).Distinct().Count()).IsEqualTo(6);
    }

    [Test]
    public async Task Flat_IsAlwaysTenWhenEvPasses()
    {
        var stake = StakeSchemes.StakeDollars("Flat", 0.58, -110, 1000);
        await Assert.That(stake).IsEqualTo(10.0);
    }

    [Test]
    public async Task BelowMinEv_ReturnsZero()
    {
        // EV at p≈0.52 @ -110 is under 4%
        var stake = StakeSchemes.StakeDollars("Kelly2", 0.52, -110, 1000);
        await Assert.That(stake).IsEqualTo(0);
    }

    [Test]
    public async Task Kelly2_CapsAtTwoPercent()
    {
        // Strong edge → fractional Kelly wants more than 2%; cap wins
        var stake = StakeSchemes.StakeDollars("Kelly2", 0.65, -110, 1000);
        await Assert.That(stake).IsEqualTo(20.0).Within(0.01);
    }

    [Test]
    public async Task Kelly5_AllowsWiderThanKelly2()
    {
        var k2 = StakeSchemes.StakeDollars("Kelly2", 0.65, -110, 1000);
        var k5 = StakeSchemes.StakeDollars("Kelly5", 0.65, -110, 1000);
        await Assert.That(k5).IsGreaterThan(k2);
        await Assert.That(k5).IsLessThanOrEqualTo(50.0);
    }

    [Test]
    public async Task ConfBand_ScalesWithDistanceFromCoinFlip()
    {
        var low = StakeSchemes.StakeDollars("ConfBand", 0.55, -110, 1000);
        var high = StakeSchemes.StakeDollars("ConfBand", 0.75, -110, 1000);
        await Assert.That(high).IsGreaterThan(low);
        await Assert.That(high).IsLessThanOrEqualTo(20.0);
        await Assert.That(low).IsGreaterThanOrEqualTo(5.0);
    }

    [Test]
    public async Task EdgeBand_NbaNeedsLargerPointEdgeThanSoccer()
    {
        var nbaSmall = StakeSchemes.StakeDollars("EdgeBand", 0.58, -110, 1000, absEdge: 4, sport: "NBA");
        var nbaBig = StakeSchemes.StakeDollars("EdgeBand", 0.58, -110, 1000, absEdge: 14, sport: "NBA");
        var mls = StakeSchemes.StakeDollars("EdgeBand", 0.58, -110, 1000, absEdge: 0.6, sport: "MLS");
        await Assert.That(nbaBig).IsGreaterThan(nbaSmall);
        await Assert.That(mls).IsGreaterThan(5.0);
    }

    [Test]
    public async Task SeedAndHorizon_MatchTrialContract()
    {
        await Assert.That(StakeTrial.SeedDollars).IsEqualTo(1000.0);
        await Assert.That(StakeTrial.HorizonEnd.Year).IsEqualTo(2026);
        await Assert.That(StakeTrial.HorizonEnd.Month).IsEqualTo(12);
        await Assert.That(StakeTrial.HorizonEnd.Day).IsEqualTo(31);
    }
}
