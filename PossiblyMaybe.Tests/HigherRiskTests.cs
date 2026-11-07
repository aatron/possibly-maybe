using PossiblyMaybe.Cli.Backtest;
using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Tests;

public class HigherRiskTests
{
    [Test]
    public async Task Registry_RegistersConformalAndKalman()
    {
        var conf = VariantRegistry.Require("+conformal");
        var kal = VariantRegistry.Require("kalman");
        await Assert.That(conf.ConformalFilter).IsTrue();
        await Assert.That(conf.UseKalmanRatings).IsFalse();
        await Assert.That(kal.UseKalmanRatings).IsTrue();
        await Assert.That(kal.ConformalFilter).IsFalse();
    }

    [Test]
    public async Task Conformal_AbstainsWhenLineInsideInterval()
    {
        // |blended−line| = 6; half-width 10 → inside → CONFORMAL skip; V0 still picks.
        var c = Nba(home: "BOS", away: "MIA", line: 220, model: 232, sigma: 19, conformalHw: 10);
        var v0 = VariantRegistry.Evaluate(VariantRegistry.V0, c);
        var cf = VariantRegistry.Evaluate(VariantRegistry.Require("+conformal"), c);
        await Assert.That(v0.Pick).IsTrue();
        await Assert.That(cf.Pick).IsFalse();
        await Assert.That(cf.SkipReason).IsEqualTo(VariantSkip.Conformal);
        await Assert.That(cf.Side).IsEqualTo("OVER");
    }

    [Test]
    public async Task Conformal_PicksWhenLineOutsideInterval()
    {
        var c = Nba(home: "BOS", away: "MIA", line: 220, model: 232, sigma: 19, conformalHw: 5);
        var cf = VariantRegistry.Evaluate(VariantRegistry.Require("+conformal"), c);
        await Assert.That(cf.Pick).IsTrue();
        await Assert.That(cf.Side).IsEqualTo("OVER");
    }

    [Test]
    public async Task Kalman_UsesKalmanModelExpWhenPresent()
    {
        // EMA model 232 → edge 6 OVER; Kalman 208 → edge −6 UNDER.
        var c = Nba(home: "BOS", away: "MIA", line: 220, model: 232, sigma: 19, kalmanExp: 208);
        var v0 = VariantRegistry.Evaluate(VariantRegistry.V0, c);
        var kal = VariantRegistry.Evaluate(VariantRegistry.Require("kalman"), c);
        await Assert.That(v0.Pick).IsTrue();
        await Assert.That(v0.Side).IsEqualTo("OVER");
        await Assert.That(kal.Pick).IsTrue();
        await Assert.That(kal.Side).IsEqualTo("UNDER");
    }

    [Test]
    public async Task PaceKalman_DivergesFromEmaAfterOneUpdate()
    {
        var ema = new PaceEfficiencyBook();
        var kal = new PaceEfficiencyKalmanBook();
        ema.Update("BOS", "MIA", 80, 10, 12, 20, 110, 25, 85, 8, 14, 22, 105, 30);
        kal.Update("BOS", "MIA", 80, 10, 12, 20, 110, 25, 85, 8, 14, 22, 105, 30);
        var d = Math.Abs(ema.ExpectedTotal("BOS", "NYK") - kal.ExpectedTotal("BOS", "NYK"));
        Console.WriteLine($"|ema-kal|={d:F4} ema={ema.ExpectedTotal("BOS","NYK"):F2} kal={kal.ExpectedTotal("BOS","NYK"):F2}");
        await Assert.That(d).IsGreaterThan(0.5);
    }

    [Test]
    public async Task LocalLevelKalman_SteadyGainMatchesChampAlpha()
    {
        var k = LocalLevelKalman.SteadyStateGain(
            HigherRiskFamily.KalmanProcessNoise, HigherRiskFamily.KalmanObsNoise);
        await Assert.That(k).IsEqualTo(0.15).Within(1e-9);
    }

    [Test]
    public async Task ConformalAbsResidual_HalfWidthIsQuantile()
    {
        var c = new ConformalAbsResidual();
        for (var i = 1; i <= 100; i++)
            c.Observe(i); // scores 1..100
        // idx = ceil(101*0.9)-1 = 90 → sorted[90] = 91
        await Assert.That(c.HalfWidth()).IsEqualTo(91);
    }

    [Test]
    public async Task TierA_KeepRule_DocumentedAgainstV0()
    {
        var db = ResolveDb();
        if (db is null)
        {
            Console.WriteLine("SKIP HigherRisk Tier A: no possiblymaybe.db");
            return;
        }

        // One mid fold as a smoke of the walk-forward path; full table is tier-a-b8 / b8_proof.md.
        var (v0, cf, kal) = await HigherRiskTierA.RunFoldAsync(
            db, "2019", ChampConfig.MarketWeight, ChampConfig.MinEv, ChampConfig.MinAbsTotalEdge);
        Console.WriteLine(
            $"fold2019 V0 n={v0.Bets} clv={v0.MeanClv:F2}  cf n={cf.Bets} clv={cf.MeanClv:F2}  kal n={kal.Bets} clv={kal.MeanClv:F2}");
        await Assert.That(v0.Bets).IsGreaterThan(0);
        // Conformal is a subset (or empty early); Kalman must produce some tickets across a full season.
        await Assert.That(kal.Bets).IsGreaterThan(0);
    }

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

    private static SlateCandidate Nba(
        string home, string away, double line, double model,
        double sigma = 19.0, double? conformalHw = null, double? kalmanExp = null)
        => new("NBA", "20261107", home, away, model, null, null,
            line, line, -110, -110, sigma,
            ChampConfig.MarketWeight, ChampConfig.MinAbsTotalEdge, ChampConfig.MinEv, false,
            ConformalHalfWidth: conformalHw, KalmanModelExp: kalmanExp);
}
