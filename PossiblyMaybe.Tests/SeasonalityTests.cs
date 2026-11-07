using Microsoft.EntityFrameworkCore;
using PossiblyMaybe.Cli.Backtest;
using PossiblyMaybe.Cli.Variants;
using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Tests;

public class SeasonalityTests
{
    [Test]
    public async Task Resolve_Nba_PriorityAndWrap()
    {
        await Assert.That(SeasonalityPhases.Resolve("NBA", "20251020")).IsEqualTo("opening-weeks");
        await Assert.That(SeasonalityPhases.Resolve("NBA", "20260115")).IsEqualTo("weather-winter");
        await Assert.That(SeasonalityPhases.Resolve("NBA", "20260320")).IsEqualTo("tanking-stretch");
        await Assert.That(SeasonalityPhases.Resolve("NBA", "20260501")).IsEqualTo("playoffs");
        await Assert.That(SeasonalityPhases.Resolve("NBA", "20250701")).IsNull();
    }

    [Test]
    public async Task Resolve_Euro_IntlBreak_BeatsOpening()
    {
        await Assert.That(SeasonalityPhases.Resolve("LIGUE1", "20250905")).IsEqualTo("intl-break");
        await Assert.That(SeasonalityPhases.Resolve("SERIEA", "20250820")).IsEqualTo("opening-weeks");
        await Assert.That(SeasonalityPhases.Resolve("LIGUE1", "20251225")).IsEqualTo("winter-break");
        await Assert.That(SeasonalityPhases.Resolve("SERIEA", "20260105")).IsEqualTo("winter-break");
    }

    [Test]
    public async Task Resolve_Mls_And_LigaMx()
    {
        await Assert.That(SeasonalityPhases.Resolve("MLS", "20260301")).IsEqualTo("opening-weeks");
        await Assert.That(SeasonalityPhases.Resolve("MLS", "20260615")).IsEqualTo("summer-intl-break");
        await Assert.That(SeasonalityPhases.Resolve("LIGAMX", "20250715")).IsEqualTo("apertura-opening");
        await Assert.That(SeasonalityPhases.Resolve("LIGAMX", "20251120")).IsEqualTo("apertura-liguilla");
    }

    [Test]
    public async Task Bh_Rejects_StrongHit_At_Q10()
    {
        // One tiny p among nulls → BH should reject the strong hit at q=0.10 when m is small.
        var p = new[] { 0.01, 0.5, 0.6, 0.7 };
        var reject = SeasonalityScan.BenjaminiHochbergReject(p, q: 0.10);
        await Assert.That(reject[0]).IsTrue();
        await Assert.That(reject[1]).IsFalse();

        var adj = SeasonalityScan.BhAdjustedPValues(p);
        await Assert.That(adj[0]).IsLessThanOrEqualTo(0.10);
        await Assert.That(adj[0]).IsLessThanOrEqualTo(adj[1]);
    }

    [Test]
    public async Task Score_Prints_NTrials_And_MeanClv()
    {
        var rng = new Random(1);
        var tagged = new List<(string, string, double)>();
        for (var i = 0; i < 30; i++)
            tagged.Add(("NBA", "opening-weeks", 2.0 + rng.NextDouble() * 0.1));
        for (var i = 0; i < 30; i++)
            tagged.Add(("NBA", "playoffs", rng.NextDouble() - 0.5));
        for (var i = 0; i < 5; i++)
            tagged.Add(("MLS", "opening-weeks", 1.0)); // below MinN → p=1

        var cells = SeasonalityScan.Score(tagged);
        await Assert.That(cells.Count).IsEqualTo(3);
        await Assert.That(cells[0].NTrials).IsEqualTo(3);

        var open = cells.Single(c => c.Sport == "NBA" && c.PhaseId == "opening-weeks");
        await Assert.That(open.MeanClv).IsGreaterThan(1.5);
        await Assert.That(open.BhSignificant).IsTrue();

        var mls = cells.Single(c => c.Sport == "MLS");
        await Assert.That(mls.PValue).IsEqualTo(1.0);
        await Assert.That(mls.N).IsEqualTo(5);

        using var sw = new StringWriter();
        SeasonalityScan.Print(cells, sw);
        var text = sw.ToString();
        await Assert.That(text).Contains("N_trials=3");
        await Assert.That(text).Contains("BH q=0.10");
    }

    [Test]
    public async Task RunAsync_TagsCandidates_FromGameKey()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pm-a5-{Guid.NewGuid():N}.db");
        try
        {
            var (db, _) = await VariantLedger.OpenAsync(path);
            await using (db)
            {
                db.ShadowEntries.Add(new ShadowEntry
                {
                    VariantId = SeasonalityScan.SignalVariantId,
                    Sport = "NBA",
                    GameKey = "20251022 BOS@NYK",
                    Side = "OVER",
                    Line = 220,
                    AmericanOdds = -110,
                    Result = "W",
                    PnL = 0.91,
                    ClvPts = 1.5,
                    EvaluatedAt = DateTimeOffset.UtcNow,
                    SettledAt = DateTimeOffset.UtcNow,
                });
                // 30 rows so MinN clears for opening-weeks
                for (var i = 0; i < 29; i++)
                {
                    db.ShadowEntries.Add(new ShadowEntry
                    {
                        VariantId = SeasonalityScan.SignalVariantId,
                        Sport = "NBA",
                        GameKey = $"202510{23 + i % 5:D2} TEAM{i}@HOME{i}",
                        Side = "OVER",
                        Line = 220,
                        Result = "W",
                        ClvPts = 1.5,
                        EvaluatedAt = DateTimeOffset.UtcNow,
                        SettledAt = DateTimeOffset.UtcNow,
                    });
                }
                db.CandidateEntries.Add(new CandidateEntry
                {
                    Sport = "NBA",
                    GameKey = "20251022 BOS@NYK",
                    Yyyymmdd = "20251022",
                    Home = "NYK",
                    Away = "BOS",
                    StoredAt = DateTimeOffset.UtcNow,
                });
                await db.SaveChangesAsync();
            }

            using var sw = new StringWriter();
            var calPath = Path.Combine(Path.GetTempPath(), $"pm-a5-cal-{Guid.NewGuid():N}.json");
            var cells = await SeasonalityScan.RunAsync(
                path, writeBackPhase: true, output: sw, calendarPath: calPath);
            await Assert.That(cells.Count).IsEqualTo(1);
            await Assert.That(cells[0].PhaseId).IsEqualTo("opening-weeks");
            await Assert.That(sw.ToString()).Contains("N_trials=1");

            var (db2, _) = await VariantLedger.OpenAsync(path);
            await using (db2)
            {
                var cand = await db2.CandidateEntries.SingleAsync();
                await Assert.That(cand.SeasonPhase).IsEqualTo("opening-weeks");
            }

            var keys = await SeasonalityCalendarStore.LoadSignificantKeysAsync(calPath);
            await Assert.That(keys.Contains(WaveRadar.SeasonPhaseKey("NBA", "opening-weeks"))).IsTrue();
            if (File.Exists(calPath)) File.Delete(calPath);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
