using Microsoft.EntityFrameworkCore;
using PossiblyMaybe.Cli.Paper;
using PossiblyMaybe.Data;
using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Tests;

/// <summary>R6: paper DK tickets carry real juice when present; assumed excluded from CLV.</summary>
public class PaperRealPriceTests
{
    [Test]
    public async Task PaperChamp_UsesRealDkJuice_WhenPresent()
    {
        var c = Cand(dkOver: -102, dkUnder: -118);
        var shadow = VariantRegistry.Evaluate(VariantRegistry.V0, c);
        var paper = VariantRegistry.Evaluate(VariantRegistry.PaperChamp, c);

        await Assert.That(shadow.AmericanOdds).IsEqualTo(-110);
        await Assert.That(shadow.AssumedPrice).IsTrue();

        await Assert.That(paper.Pick).IsTrue();
        await Assert.That(paper.Side).IsEqualTo("OVER");
        await Assert.That(paper.AmericanOdds).IsEqualTo(-102);
        await Assert.That(paper.AssumedPrice).IsFalse();
        await Assert.That(paper.Ev).IsGreaterThan(shadow.Ev);
    }

    [Test]
    public async Task PaperChamp_MarksAssumed_WhenDkMissing()
    {
        var c = Cand(dkOver: null, dkUnder: null);
        var paper = VariantRegistry.Evaluate(VariantRegistry.PaperChamp, c);
        await Assert.That(paper.Pick).IsTrue();
        await Assert.That(paper.AmericanOdds).IsEqualTo(-110);
        await Assert.That(paper.AssumedPrice).IsTrue();
    }

    [Test]
    public async Task Place_AssumedJuice_ExcludedFromClv_AndRealPriceStats()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pm-r6-{Guid.NewGuid():N}.db");
        try
        {
            var (db, _) = await PaperLedger.OpenAsync(path);
            await using (db)
            {
                var real = await PaperLedger.PlaceAllSchemesAsync(
                    db, "20261107 MIA@BOS", "OVER", 220, -105, 0.58, 6, "NBA",
                    PaperVenues.DraftKings, assumedJuice: false);
                await Assert.That(real.Count).IsGreaterThan(0);
                await Assert.That(real.All(r => !r.AssumedJuice)).IsTrue();

                // Second game key so place isn't skipped as already-open.
                var assumed = await PaperLedger.PlaceAllSchemesAsync(
                    db, "20261107 NYK@CHI", "UNDER", 215, -110, 0.58, 6, "NBA",
                    PaperVenues.DraftKings, assumedJuice: true);
                await Assert.That(assumed.Count).IsGreaterThan(0);
                await Assert.That(assumed.All(r => r.AssumedJuice)).IsTrue();

                foreach (var r in real.Concat(assumed))
                    PaperLedger.ApplySettle(r, "W", finalTotal: 230, closeLine: 222, closeAmerican: -105);

                await db.SaveChangesAsync();

                await Assert.That(real.All(r => r.ClvPts == 2.0)).IsTrue(); // 222 − 220
                await Assert.That(assumed.All(r => r.ClvPts is null)).IsTrue();

                var (clvN, _) = await PaperLedger.LiveClvStatsAsync(db);
                await Assert.That(clvN).IsEqualTo(real.Count);

                var (total, realN, pct) = await PaperLedger.DkRealPriceStatsAsync(db);
                await Assert.That(total).IsEqualTo(real.Count + assumed.Count);
                await Assert.That(realN).IsEqualTo(real.Count);
                await Assert.That(pct).IsGreaterThan(0);
                await Assert.That(pct).IsLessThan(100);
            }
        }
        finally
        {
            try { File.Delete(path); } catch { /* */ }
        }
    }

    static SlateCandidate Cand(int? dkOver, int? dkUnder)
        => new("NBA", "20261107", "BOS", "MIA", 232, null, null,
            220, 220, dkOver, dkUnder, 19,
            ChampConfig.MarketWeight, ChampConfig.MinAbsTotalEdge, ChampConfig.MinEv, false);
}
