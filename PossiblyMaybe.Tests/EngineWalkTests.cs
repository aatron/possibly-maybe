using PossiblyMaybe.Cli.Import;
using PossiblyMaybe.Cli.Replay;
using PossiblyMaybe.Data;
using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Tests;

public class EngineWalkTests
{
    /// <summary>
    /// T7 proof: 3-day synthetic walk — day-2 Decide cannot see day-2 finals or close lines.
    /// </summary>
    [Test]
    public async Task ThreeDay_Walk_Day2_Decide_Cannot_See_Day2_Finals_Or_Closes()
    {
        // Day1 tip 18:00Z, Day2 tip 18:00Z, Day3 tip 18:00Z. SlateAsOf = midnight UTC.
        var d1 = "20261101";
        var d2 = "20261102";
        var d3 = "20261103";
        var tip1 = DateTimeOffset.Parse("2026-11-01T18:00:00Z");
        var tip2 = DateTimeOffset.Parse("2026-11-02T18:00:00Z");
        var tip3 = DateTimeOffset.Parse("2026-11-03T18:00:00Z");

        var final1 = new FinalGameRow(1, "NBA", "2027", tip1, "BOS", "MIA", 110, 100); // total 210
        var final2 = new FinalGameRow(2, "NBA", "2027", tip2, "NYK", "CHI", 120, 115); // total 235
        var final3 = new FinalGameRow(3, "NBA", "2027", tip3, "LAL", "DEN", 105, 99);  // total 204

        var open1 = new OddsAsOfRow(10, 1, "DraftKings", "TOTAL", "over", -110, 220, tip1.AddDays(-7));
        var close1 = new OddsAsOfRow(11, 1, "DraftKings", "TOTAL", "over", -110, 218, tip1);
        var open2 = new OddsAsOfRow(20, 2, "DraftKings", "TOTAL", "over", -110, 225, tip2.AddDays(-7));
        var close2 = new OddsAsOfRow(21, 2, "DraftKings", "TOTAL", "over", -110, 230, tip2);
        var open3 = new OddsAsOfRow(30, 3, "DraftKings", "TOTAL", "over", -110, 215, tip3.AddDays(-7));
        var close3 = new OddsAsOfRow(31, 3, "DraftKings", "TOTAL", "over", -110, 210, tip3);

        // Decision views: midnight of each day — prior finals only; opens yes; closes for that day no.
        var view1 = AsOfViewFactory.FromSnapshots(
            PointInTime.SlateAsOf(d1),
            finals: [],
            odds: [open1, open2, open3]); // closes not yet (tip in future); also hide future opens? opens have CapturedAt tip-7d ≤ asOf
        var view2 = AsOfViewFactory.FromSnapshots(
            PointInTime.SlateAsOf(d2),
            finals: [final1],
            odds: [open1, close1, open2, open3]); // close2/close3 at tip2/tip3 > asOf → filtered out
        var view3 = AsOfViewFactory.FromSnapshots(
            PointInTime.SlateAsOf(d3),
            finals: [final1, final2],
            odds: [open1, close1, open2, close2, open3]);

        // Day-2 view must not contain day-2 final or close.
        await Assert.That(view2.FinalGames().Any(g => g.Id == 2)).IsFalse();
        await Assert.That(view2.OddsSnapshots().Any(o => o.Id == 21)).IsFalse();
        await Assert.That(view2.FinalGames().Any(g => g.Id == 1)).IsTrue();
        await Assert.That(view2.OddsSnapshots().Any(o => o.Id == 11)).IsTrue(); // prior close ok
        await Assert.That(view2.OddsSnapshots().Any(o => o.Id == 20)).IsTrue(); // day-2 open ok

        SlateCandidate Cand(string day, string home, string away, double line, double model)
            => new("NBA", day, home, away, model, null, null,
                line, line, -110, -110, ExpandingResidualSigma.FallbackSigma,
                ChampConfig.MarketWeight, ChampConfig.MinAbsTotalEdge, ChampConfig.MinEv, false);

        // Force picks: model well above line → OVER.
        var c1 = Cand(d1, "BOS", "MIA", 220, 240);
        var c2 = Cand(d2, "NYK", "CHI", 225, 245);
        var c3 = Cand(d3, "LAL", "DEN", 215, 235);
        var oregon = Cand(d2, "POR", "LAL", 220, 240); // must be excluded

        var days = new List<(string, AsOfView, IReadOnlyList<SlateCandidate>, IReadOnlyList<FinalGameRow>, IReadOnlyDictionary<string, (double, Book)>)>
        {
            (d1, view1, [c1], [final1], new Dictionary<string, (double, Book)>
            {
                [VariantRegistry.GameKey(c1)] = (218, Book.DraftKings),
            }),
            (d2, view2, [c2, oregon], [final2], new Dictionary<string, (double, Book)>
            {
                [VariantRegistry.GameKey(c2)] = (230, Book.DraftKings),
            }),
            (d3, view3, [c3], [final3], new Dictionary<string, (double, Book)>
            {
                [VariantRegistry.GameKey(c3)] = (210, Book.DraftKings),
            }),
        };

        var report = EngineWalk.RunSynthetic(
            new DateOnly(2026, 11, 1),
            new DateOnly(2026, 11, 3),
            days,
            strategies: ["V0-paper"]);

        await Assert.That(report.DaysWalked).IsEqualTo(3);
        await Assert.That(report.BeforeStrategySha).IsEqualTo(EngineWalk.Round4CloseSha);
        await Assert.That(report.OregonExcludedGames).IsEqualTo(1);

        // Day-2 Decide used the open (225), not the close (230); CLV after reveal = +5 OVER.
        var day2Flat = report.Tickets.Where(t =>
            t.Yyyymmdd == d2 && t.Venue == PaperVenues.DraftKings && t.Scheme == "Flat"
            && t.Away == "CHI").ToList();
        await Assert.That(day2Flat.Count).IsEqualTo(1);
        await Assert.That(day2Flat[0].Line).IsEqualTo(225);
        await Assert.That(day2Flat[0].CloseLine).IsEqualTo(230);
        await Assert.That(day2Flat[0].ClvPts).IsEqualTo(5.0);
        await Assert.That(day2Flat[0].SameBookClose).IsTrue();
        await Assert.That(day2Flat[0].ClvFlagged).IsFalse();

        // Oregon never bet.
        await Assert.That(report.Tickets.Any(t => t.Home == "POR" || t.Away == "POR")).IsFalse();

        // Day-2 Decide under view2: strategy never sees close2 in the watermark odds set.
        var day2Decide = DayEngine.Run(d2, VariantStrategy.Resolve("V0-paper"), view2, [c2, oregon]);
        await Assert.That(day2Decide.Decisions.Count(x => x.Pick)).IsEqualTo(1);
        await Assert.That(day2Decide.Decisions.Single(x => x.Pick).Line).IsEqualTo(225);
        await Assert.That(view2.OddsSnapshots().Any(o => o.Id == close2.Id)).IsFalse();
        await Assert.That(view2.FinalGames().Any(g => g.Id == final2.Id)).IsFalse();
    }

    [Test]
    public async Task ReplayShouldSkip_CloseOnly_StillHonored()
    {
        await Assert.That(EspnLineBackfill.ReplayShouldSkip(Array.Empty<OddsSnapshot>())).IsTrue();
        var open = new[]
        {
            new OddsSnapshot
            {
                Market = "TOTAL", PriceKind = PriceKind.Open, Line = 2.5, AmericanOdds = -110,
            },
        };
        await Assert.That(EspnLineBackfill.ReplayShouldSkip(open)).IsFalse();
    }

    [Test]
    public async Task VariantStrategy_Resolves_PaperChamp_And_ShadowIds()
    {
        var paper = VariantStrategy.Resolve("V0-paper");
        await Assert.That(paper.Id).IsEqualTo("V0-paper");
        var shadow = VariantStrategy.Resolve("kalman");
        await Assert.That(shadow.Id).IsEqualTo("kalman");
        var v0 = VariantStrategy.Resolve("V0");
        await Assert.That(v0.Id).IsEqualTo("V0");
    }
}
