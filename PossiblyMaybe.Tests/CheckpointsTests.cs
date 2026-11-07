using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Tests;

public class CheckpointsTests
{
    [Test]
    public async Task StageFor_Boundaries()
    {
        await Assert.That(Checkpoints.StageFor(0)).IsEqualTo(CheckpointStage.TooSoon);
        await Assert.That(Checkpoints.StageFor(49)).IsEqualTo(CheckpointStage.TooSoon);
        await Assert.That(Checkpoints.StageFor(50)).IsEqualTo(CheckpointStage.BailCheck);
        await Assert.That(Checkpoints.StageFor(99)).IsEqualTo(CheckpointStage.BailCheck);
        await Assert.That(Checkpoints.StageFor(100)).IsEqualTo(CheckpointStage.ClvVerdict);
        await Assert.That(Checkpoints.StageFor(200)).IsEqualTo(CheckpointStage.ClvVerdict);
        await Assert.That(Checkpoints.StageFor(499)).IsEqualTo(CheckpointStage.ClvVerdict);
        await Assert.That(Checkpoints.StageFor(500)).IsEqualTo(CheckpointStage.PnlLoose);
        await Assert.That(Checkpoints.StageFor(1499)).IsEqualTo(CheckpointStage.PnlLoose);
        await Assert.That(Checkpoints.StageFor(1500)).IsEqualTo(CheckpointStage.PnlConfirms);
        await Assert.That(Checkpoints.StageFor(10_000)).IsEqualTo(CheckpointStage.PnlConfirms);
    }

    [Test]
    public async Task BailCheck_OnlyWhenClearlyNegative()
    {
        // mean=-1, SE=0.2 → upper CI ≈ -0.61 < 0 → bail
        var bail = Checkpoints.Evaluate(60, -1.0, 0.2);
        await Assert.That(bail.Stage).IsEqualTo(CheckpointStage.BailCheck);
        await Assert.That(bail.Verdict).IsEqualTo("bail");

        // mean=-0.1, SE=0.2 → upper CI ≈ +0.29 → hold
        var hold = Checkpoints.Evaluate(60, -0.1, 0.2);
        await Assert.That(hold.Verdict).IsEqualTo("hold");
    }

    [Test]
    public async Task ClvVerdict_KeepOrStop()
    {
        var keep = Checkpoints.Evaluate(150, 0.3, 0.1);
        await Assert.That(keep.Stage).IsEqualTo(CheckpointStage.ClvVerdict);
        await Assert.That(keep.Verdict).IsEqualTo("keep");

        var stop = Checkpoints.Evaluate(150, -0.1, 0.05);
        await Assert.That(stop.Verdict).IsEqualTo("stop");
    }

    [Test]
    public async Task WideStake_OnlyAtPnlConfirmsWithPositiveMean()
    {
        await Assert.That(Checkpoints.AllowsWideStake(1499, 1.0)).IsFalse();
        await Assert.That(Checkpoints.AllowsWideStake(1500, 0)).IsFalse();
        await Assert.That(Checkpoints.AllowsWideStake(1500, 0.01)).IsTrue();

        var ok = Checkpoints.Evaluate(1500, 0.2, 0.05);
        await Assert.That(ok.Stage).IsEqualTo(CheckpointStage.PnlConfirms);
        await Assert.That(ok.Verdict).IsEqualTo("pnl-ok");
        await Assert.That(ok.AllowsWideStake).IsTrue();
    }

    [Test]
    public async Task Evaluate_FromSeries_ReportsSe()
    {
        var pts = Enumerable.Repeat(2.0, 50).Concat(Enumerable.Repeat(0.0, 50)).ToList();
        var r = Checkpoints.Evaluate(pts);
        await Assert.That(r.N).IsEqualTo(100);
        await Assert.That(r.Stage).IsEqualTo(CheckpointStage.ClvVerdict);
        await Assert.That(r.ClvSe).IsGreaterThan(0);
        await Assert.That(r.MeanClv).IsEqualTo(1.0);
    }

    [Test]
    public async Task LeaderPersistence_MatchesA3()
    {
        var weeks = Checkpoints.LeaderPersistenceWeeks;
        await Assert.That(weeks).IsEqualTo(WaveRadar.PersistenceWeeks);
        await Assert.That(weeks).IsEqualTo(3);
    }
}
