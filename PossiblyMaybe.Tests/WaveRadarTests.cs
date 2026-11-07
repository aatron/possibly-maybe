using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Tests;

public class WaveRadarTests
{
    [Test]
    public async Task Injected_Plus2_Clv_Wave_Detected_Within_60()
    {
        var rng = new Random(42);
        var t0 = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var obs = new List<ClvObservation>(60);
        for (var i = 0; i < 60; i++)
        {
            // +2pt CLV wave with σ≈3 noise
            var clv = 2.0 + NextGaussian(rng) * 3.0;
            obs.Add(new ClvObservation("NBA", "TOTAL", "V0", t0.AddDays(i), clv));
        }

        var result = WaveRadar.Scan(obs, asOf: t0.AddDays(59)).Single();
        await Assert.That(result.Wave).IsEqualTo(WaveState.On);
        await Assert.That(result.CusumFirstAlarmIndex).IsNotNull();
        await Assert.That(result.CusumFirstAlarmIndex!.Value).IsLessThan(60);
        await Assert.That(result.BhSignificant).IsTrue();
        await Assert.That(result.NTrials).IsEqualTo(1);
    }

    [Test]
    public async Task Null_Cells_FalseAlarm_AtMost_10pct()
    {
        var rng = new Random(7);
        var t0 = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        const int nCells = 100;
        const int nBets = 80;
        var obs = new List<ClvObservation>(nCells * nBets);
        for (var c = 0; c < nCells; c++)
        {
            for (var i = 0; i < nBets; i++)
            {
                var clv = NextGaussian(rng) * 3.0; // mean 0
                obs.Add(new ClvObservation($"L{c:D3}", "TOTAL", "V0", t0.AddDays(i), clv));
            }
        }

        var results = WaveRadar.Scan(obs, asOf: t0.AddDays(nBets));
        await Assert.That(results.Count).IsEqualTo(nCells);
        await Assert.That(results[0].NTrials).IsEqualTo(nCells);

        var faBh = results.Count(r => r.BhSignificant);
        var faWave = results.Count(r => r.Wave == WaveState.On);
        await Assert.That(faBh / (double)nCells).IsLessThanOrEqualTo(0.10);
        await Assert.That(faWave / (double)nCells).IsLessThanOrEqualTo(0.10);
    }

    [Test]
    public async Task Bh_Rejects_Strong_Signal_Among_Nulls()
    {
        var rng = new Random(99);
        var t0 = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var obs = new List<ClvObservation>();
        for (var c = 0; c < 40; c++)
        {
            for (var i = 0; i < 50; i++)
                obs.Add(new($"N{c}", "TOTAL", "V0", t0.AddDays(i), NextGaussian(rng) * 3.0));
        }
        for (var i = 0; i < 50; i++)
            obs.Add(new("WAVE", "TOTAL", "V3", t0.AddDays(i), 2.0 + NextGaussian(rng) * 2.5));

        var results = WaveRadar.Scan(obs, asOf: t0.AddDays(49));
        var hit = results.Single(r => r.League == "WAVE");
        await Assert.That(hit.BhSignificant).IsTrue();
        await Assert.That(hit.Wave).IsEqualTo(WaveState.On);
        await Assert.That(hit.NTrials).IsEqualTo(41);
    }

    [Test]
    public async Task Ladder_Promotes_After_Persistence_Demotes_On_Neg_Roll()
    {
        var t0 = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        // Steady +2 for 30 days → alarm early; asOf 4 weeks later → promote.
        var obs = Enumerable.Range(0, 40)
            .Select(i => new ClvObservation("MLS", "TOTAL", "V3", t0.AddDays(i), 2.0))
            .ToList();
        var asOf = t0.AddDays(40 + 21); // ≥3 weeks after first bets / alarm
        var promoted = WaveRadar.Scan(obs, asOf: asOf).Single();
        await Assert.That(promoted.Wave).IsEqualTo(WaveState.On);
        await Assert.That(promoted.BhSignificant).IsTrue();
        await Assert.That(promoted.WeeksOn).IsGreaterThanOrEqualTo(WaveRadar.PersistenceWeeks);
        await Assert.That(promoted.Stage).IsEqualTo(LadderStage.Paper);

        // Append negative rolling window → demote from paper.
        var key = WaveRadar.CellKey("MLS", "TOTAL", "V3");
        var crash = obs.Concat(Enumerable.Range(0, WaveRadar.RollingWindow)
            .Select(i => new ClvObservation("MLS", "TOTAL", "V3", asOf.AddDays(i + 1), -1.0)))
            .ToList();
        var demoted = WaveRadar.Scan(
            crash,
            priorStages: new Dictionary<string, LadderStage> { [key] = LadderStage.Paper },
            asOf: asOf.AddDays(WaveRadar.RollingWindow + 1)).Single();
        await Assert.That(demoted.Wave).IsEqualTo(WaveState.Off);
        await Assert.That(demoted.Stage).IsEqualTo(LadderStage.Shadow);
        await Assert.That(demoted.Demote).IsTrue();
    }

    [Test]
    public async Task Paper_To_Real_Never_Auto()
    {
        var t0 = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var obs = Enumerable.Range(0, 40)
            .Select(i => new ClvObservation("NBA", "TOTAL", "V0", t0.AddDays(i), 2.5))
            .ToList();
        var key = WaveRadar.CellKey("NBA", "TOTAL", "V0");
        var r = WaveRadar.Scan(
            obs,
            priorStages: new Dictionary<string, LadderStage> { [key] = LadderStage.Paper },
            asOf: t0.AddDays(60)).Single();
        await Assert.That(r.Stage).IsEqualTo(LadderStage.Paper); // not RealCandidate
    }

    static double NextGaussian(Random rng)
    {
        // Box–Muller
        var u1 = 1.0 - rng.NextDouble();
        var u2 = 1.0 - rng.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }
}
