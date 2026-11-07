using PossiblyMaybe.Cli.Replay;
using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Tests;

public class DayEngineTests
{
    static readonly DateTimeOffset T = DateTimeOffset.Parse("2026-11-07T00:00:00Z");
    const string Day = "20261107";

    /// <summary>
    /// R9 proof: engine V0 decisions on a synthetic live slate == VariantRegistry.Evaluate(V0).
    /// Same candidates DailySlate would emit → DayEngine.Run must match the live filter path.
    /// </summary>
    [Test]
    public async Task Engine_V0_Matches_Live_Evaluate_On_Synthetic_Slate()
    {
        var slate = new List<SlateCandidate>
        {
            NbaCand("BOS", "MIA", line: 220, model: 232, sigma: 19),
            NbaCand("BOS", "NYK", line: 218, model: 210, sigma: 19),
            SoccerCand("MLS", "LAFC", "SEA", 2.5, 1.8, 1.5),
            NbaCand("POR", "LAL", line: 220, model: 230), // Oregon — must stay EXCLUDED
        };

        var prior = new FinalGameRow(1, "NBA", "2025", DateTimeOffset.Parse("2026-11-01T00:00:00Z"),
            "BOS", "CHA", 110, 100);
        var view = AsOfViewFactory.FromSnapshots(T, finals: [prior], seasonPhase: "opening-weeks");

        var result = DayEngine.Run(Day, StrategyRegistry.V0, view, slate);

        await Assert.That(result.StrategyId).IsEqualTo("V0");
        await Assert.That(result.AsOf).IsEqualTo(T);
        await Assert.That(result.Decisions.Count).IsEqualTo(slate.Count);
        await Assert.That(result.ViewWatermark).IsEqualTo(view.Watermark());

        for (var i = 0; i < slate.Count; i++)
        {
            var expected = VariantRegistry.Evaluate(VariantRegistry.V0, slate[i]);
            var got = result.Decisions[i];
            await Assert.That(got.GameKey).IsEqualTo(VariantRegistry.GameKey(slate[i]));
            await AssertDecisionEqual(expected, got);
        }

        await Assert.That(result.Decisions[3].SkipReason).IsEqualTo(VariantSkip.Excluded);
        await Assert.That(result.Decisions[3].Pick).IsFalse();
        await Assert.That(result.ReportByLeague.Select(r => r.League)).Contains("NBA");
    }

    [Test]
    public async Task Engine_Uses_SlateAsOf_Cutoff()
    {
        var asOf = PointInTime.SlateAsOf(Day);
        await Assert.That(asOf).IsEqualTo(T);
        var view = new AsOfView(asOf);
        var result = DayEngine.Run(Day, StrategyRegistry.V0, view, []);
        await Assert.That(result.AsOf).IsEqualTo(asOf);
        await Assert.That(result.Decisions).IsEmpty();
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

    static SlateCandidate NbaCand(
        string home, string away, double line, double model, double sigma = 19.0)
        => new("NBA", Day, home, away, model, null, null,
            line, line, -110, -110, sigma,
            ChampConfig.MarketWeight, ChampConfig.MinAbsTotalEdge, ChampConfig.MinEv, false);

    static SlateCandidate SoccerCand(
        string sport, string home, string away, double line, double lh, double la)
        => new(sport, Day, home, away, lh + la, lh, la,
            line, line, -110, -110, 0,
            MlsChampConfig.MarketWeight, MlsChampConfig.MinAbsTotalEdge, MlsChampConfig.MinEv, false);
}
