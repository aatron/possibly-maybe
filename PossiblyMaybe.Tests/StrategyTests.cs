using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Tests;

public class StrategyTests
{
    static readonly DateTimeOffset T = DateTimeOffset.Parse("2026-11-07T12:00:00Z");

    [Test]
    public async Task Registry_Has_V0()
    {
        await Assert.That(StrategyRegistry.All.Select(s => s.Id)).Contains("V0");
        await Assert.That(StrategyRegistry.V0.Id).IsEqualTo("V0");
        await Assert.That(StrategyRegistry.Require("V0")).IsSameReferenceAs(StrategyRegistry.V0);
    }

    /// <summary>R8 proof: V0 strategy matches VariantRegistry.Evaluate(V0) unit-for-unit on NBA fixture.</summary>
    [Test]
    public async Task V0_Decide_Matches_VariantRegistry_V0_NbaFixture()
    {
        var c = NbaCand(home: "BOS", away: "MIA", line: 220, model: 232, open: 220, sigma: 19);
        var expected = VariantRegistry.Evaluate(VariantRegistry.V0, c);
        var view = new AsOfView(T);
        var got = StrategyRegistry.V0.Decide(new StrategySlate([c]), view).Single();

        await Assert.That(got.GameKey).IsEqualTo(VariantRegistry.GameKey(c));
        await AssertDecisionEqual(expected, got);

        await Assert.That(got.Pick).IsTrue();
        await Assert.That(got.Side).IsEqualTo("OVER");
        await Assert.That(got.AmericanOdds).IsEqualTo(-110);
        await Assert.That(got.AbsEdge).IsEqualTo(6.0);
        await Assert.That(got.SkipReason).IsNull();
    }

    [Test]
    public async Task V0_Decide_Matches_VariantRegistry_V0_SoccerFixture()
    {
        var c = SoccerCand("MLS", "LAFC", "SEA", 2.5, 1.8, 1.5);
        var expected = VariantRegistry.Evaluate(VariantRegistry.V0, c);
        var got = StrategyRegistry.V0.Decide(new StrategySlate([c]), new AsOfView(T)).Single();
        await AssertDecisionEqual(expected, got);
    }

    [Test]
    public async Task V0_Decide_Oregon_Excluded_Matches_Evaluate()
    {
        var c = NbaCand(home: "POR", away: "LAL", line: 220, model: 230);
        var expected = VariantRegistry.Evaluate(VariantRegistry.V0, c);
        var got = StrategyRegistry.V0.Decide(new StrategySlate([c]), new AsOfView(T)).Single();

        await Assert.That(expected.SkipReason).IsEqualTo(VariantSkip.Excluded);
        await AssertDecisionEqual(expected, got);
    }

    [Test]
    public async Task V0_Decide_MultiCandidate_OrderPreserved_And_Learn_Noop()
    {
        var a = NbaCand(home: "BOS", away: "MIA", line: 220, model: 232, sigma: 19);
        var b = NbaCand(home: "BOS", away: "NYK", line: 218, model: 210, sigma: 19);
        var strategy = new V0TotalsStrategy();
        strategy.Learn([
            new FinalGameRow(1, "NBA", "2025", DateTimeOffset.Parse("2026-11-01T00:00:00Z"),
                "BOS", "CHA", 110, 100),
        ]);

        var decisions = strategy.Decide(new StrategySlate([a, b]), new AsOfView(T));
        await Assert.That(decisions.Count).IsEqualTo(2);
        await AssertDecisionEqual(VariantRegistry.Evaluate(VariantRegistry.V0, a), decisions[0]);
        await AssertDecisionEqual(VariantRegistry.Evaluate(VariantRegistry.V0, b), decisions[1]);
    }

    static async Task AssertDecisionEqual(VariantDecision expected, StrategyDecision got)
    {
        await Assert.That(got.Pick).IsEqualTo(expected.Pick);
        await Assert.That(got.Side).IsEqualTo(expected.Side);
        await Assert.That(got.Line).IsEqualTo(expected.Line);
        await Assert.That(got.AmericanOdds).IsEqualTo(expected.AmericanOdds);
        await Assert.That(got.ModelProb).IsEqualTo(expected.ModelProb);
        await Assert.That(got.Ev).IsEqualTo(expected.Ev);
        await Assert.That(got.AbsEdge).IsEqualTo(expected.AbsEdge);
        await Assert.That(got.SkipReason).IsEqualTo(expected.SkipReason);
        await Assert.That(got.AssumedPrice).IsEqualTo(expected.AssumedPrice);
    }

    private static SlateCandidate NbaCand(
        string home, string away, double line, double model,
        double? open = null, double sigma = 19.0,
        int? dkOver = -110, int? dkUnder = -110)
        => new("NBA", "20261107", home, away, model, null, null,
            line, open ?? line, dkOver, dkUnder, sigma,
            ChampConfig.MarketWeight, ChampConfig.MinAbsTotalEdge, ChampConfig.MinEv, false);

    private static SlateCandidate SoccerCand(
        string sport, string home, string away, double line, double lh, double la)
        => new(sport, "20261107", home, away, lh + la, lh, la,
            line, line, -110, -110, 0,
            MlsChampConfig.MarketWeight, MlsChampConfig.MinAbsTotalEdge, MlsChampConfig.MinEv, false);
}
