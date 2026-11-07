using PossiblyMaybe.Cli.Backtest;
using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Tests;

/// <summary>
/// Historical proof: removed bets under recommended guards lose (PnL or CLV) in a majority of Tier A folds.
/// Skips when the warehouse DB is absent (CI without data).
/// </summary>
public class GuardProofTests
{
    static string? ResolveDb()
    {
        var here = AppContext.BaseDirectory;
        foreach (var rel in new[]
                 {
                     "possiblymaybe.db",
                     Path.Combine("..", "..", "..", "..", "possiblymaybe.db"),
                     Path.Combine("..", "..", "..", "..", "..", "possibly-maybe", "possiblymaybe.db"),
                 })
        {
            var p = Path.GetFullPath(Path.Combine(here, rel));
            if (File.Exists(p)) return p;
        }

        var env = Environment.GetEnvironmentVariable("PM_DB");
        return env is not null && File.Exists(env) ? env : null;
    }

    [Test]
    public async Task NbaLineMove_RemovedBetsNegativeInMajorityOfFolds()
    {
        var db = ResolveDb();
        if (db is null)
        {
            Console.WriteLine("SKIP GuardProof: no possiblymaybe.db");
            return;
        }

        var (neg, withRem, total, remPnl) =
            await GuardSweepTierA.NbaLineMoveProofAsync(db, Guards.RecommendedNbaLineMoveK);
        Console.WriteLine(
            $"NBA lineMoveK={Guards.RecommendedNbaLineMoveK} negFolds={neg}/{withRem} (of {total}) removedPnl={remPnl:F1}");
        await Assert.That(withRem).IsGreaterThan(0);
        await Assert.That(neg * 2).IsGreaterThan(withRem);
        await Assert.That(remPnl).IsLessThan(0);
    }

    [Test]
    public async Task MlsEdgeCap_RemovedBetsNegativeInMajorityOfFolds()
    {
        var db = ResolveDb();
        if (db is null)
        {
            Console.WriteLine("SKIP GuardProof: no possiblymaybe.db");
            return;
        }

        // NBA edge-cap alone fails historical proof (high-EV removals still +PnL/+CLV);
        // MLS Tier A is the registered edge-cap proof league for RecommendedEdgeCap.
        var folds = new[] { "2019", "2020", "2021", "2022", "2023", "2024" };
        var neg = 0;
        var withRem = 0;
        double remPnl = 0;
        foreach (var season in folds)
        {
            var (_, removed) = await SoccerLeagueTotalsBacktest.RunSeasonFoldGuardedAsync(
                db, "MLS", season, "2025",
                MlsChampConfig.MarketWeight, MlsChampConfig.MinEv, MlsChampConfig.MinAbsTotalEdge,
                edgeCap: Guards.RecommendedEdgeCap, lineMoveK: null,
                useDixonColes: false, alpha: MlsChampConfig.EmaAlpha);
            if (removed.Bets == 0) continue;
            withRem++;
            remPnl += removed.PaperPnlUnits;
            if (removed.PaperPnlUnits < 0 || removed.MeanClv < 0)
                neg++;
        }

        Console.WriteLine(
            $"MLS edgeCap={Guards.RecommendedEdgeCap:P0} negFolds={neg}/{withRem} removedPnl={remPnl:F1}");
        await Assert.That(withRem).IsGreaterThan(0);
        await Assert.That(neg * 2).IsGreaterThan(withRem);
    }
}
