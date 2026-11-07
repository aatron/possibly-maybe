using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Tests;

public class ShutoffTests
{
    [Test]
    public async Task RollingClv_RequiresFullWindow()
    {
        var shortWin = Enumerable.Repeat(-1.0, Guards.ShutoffWindow - 1).ToList();
        await Assert.That(Guards.LeaguePausedByRollingClv(shortWin)).IsFalse();
        var neg = Enumerable.Repeat(-0.5, Guards.ShutoffWindow).ToList();
        await Assert.That(Guards.LeaguePausedByRollingClv(neg)).IsTrue();
        var pos = Enumerable.Repeat(0.1, Guards.ShutoffWindow).ToList();
        await Assert.That(Guards.LeaguePausedByRollingClv(pos)).IsFalse();
        var zero = Enumerable.Repeat(0.0, Guards.ShutoffWindow).ToList();
        await Assert.That(Guards.LeaguePausedByRollingClv(zero)).IsFalse(); // resume at ≥ 0
    }

    [Test]
    public async Task Sim_CutsDrawdownWithoutWorsePnl_OnBadStreak()
    {
        // 40 warm-up winners, then 40 losers (neg CLV), then recovery winners.
        var bets = new List<(double Clv, double Pnl)>();
        for (var i = 0; i < Guards.ShutoffWindow; i++)
            bets.Add((+1.0, +0.91));
        for (var i = 0; i < Guards.ShutoffWindow; i++)
            bets.Add((-1.0, -1.0));
        for (var i = 0; i < Guards.ShutoffWindow; i++)
            bets.Add((+1.0, +0.91));

        var v3 = ShutoffSim.Run(bets, shutoff: false);
        var v4 = ShutoffSim.Run(bets, shutoff: true);
        await Assert.That(v4.MaxDrawdown).IsLessThan(v3.MaxDrawdown);
        await Assert.That(v4.Pnl).IsGreaterThanOrEqualTo(v3.Pnl);
        await Assert.That(v4.Taken).IsLessThan(v3.Taken);
    }
}
