using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Tests;

public class TotalsMathTests
{
    [Test]
    public async Task OuResult_and_over_clv()
    {
        await Assert.That(TotalsMath.OuResult(110, 105, 220)).IsEqualTo(-1);
        await Assert.That(TotalsMath.OuResult(120, 110, 220)).IsEqualTo(1);
        await Assert.That(TotalsMath.OverClvPoints(220, 224)).IsEqualTo(4.0);
    }

    [Test]
    public async Task ScoringBook_moves_toward_observed()
    {
        var book = new ScoringBook(alpha: 1.0, leagueInit: 200);
        book.Update("AAA", "BBB", 130, 90);
        var exp = book.ExpectedTotal("AAA", "BBB");
        await Assert.That(exp).IsGreaterThan(200);
    }

    [Test]
    public async Task DixonColesBook_expected_total_moves_with_goals()
    {
        var book = new DixonColesBook(alpha: 1.0, leagueAvgGoals: 1.35, homeAdv: 1.25);
        var before = book.ExpectedTotal("AAA", "BBB");
        await Assert.That(before).IsEqualTo(1.35 * 1.25 + 1.35).Within(1e-9);
        book.Update("AAA", "BBB", 4, 3);
        var after = book.ExpectedTotal("AAA", "BBB");
        await Assert.That(after).IsGreaterThan(before);
        await Assert.That(after).IsGreaterThan(5.0);
        await Assert.That(after).IsLessThan(10.0); // α=1 + √-split ≈ recovers 4+3
        var (lh, la) = book.ExpectedLambdas("AAA", "BBB");
        await Assert.That(lh + la).IsEqualTo(after).Within(1e-9);
        await Assert.That(lh).IsGreaterThan(la);
    }

    [Test]
    public async Task Possessions_and_pace_book()
    {
        var poss = PaceEfficiencyBook.Possessions(fga: 90, oreb: 10, tov: 12, fta: 20);
        await Assert.That(poss).IsEqualTo(90 - 10 + 12 + 0.44 * 20).Within(1e-9);
        var book = new PaceEfficiencyBook(alpha: 1.0);
        book.Update("AAA", "BBB", 90, 10, 12, 20, 120, 35, 85, 8, 14, 18, 100, 30);
        var exp = book.ExpectedTotal("AAA", "BBB");
        await Assert.That(exp).IsGreaterThan(180);
        await Assert.That(exp).IsLessThan(260);
    }


    [Test]
    public async Task NflTotalFromEpa_off_minus_def()
    {
        // 45.2 + 7*(0.1+0.1 - 0.05 - 0.05) = 45.2 + 0.7
        var exp = TotalsMath.NflTotalFromEpa(0.1, 0.05, 0.1, 0.05);
        await Assert.That(exp).IsEqualTo(45.9).Within(1e-9);
    }

    [Test]
    public async Task Efg_prior_key_and_adj()
    {
        await Assert.That(EfgPriorMath.PriorFileKey(2025)).IsEqualTo("2023_24");
        await Assert.That(EfgPriorMath.AdjPts(0.55, 0.55, 0.54)).IsEqualTo(2.0).Within(1e-9);
    }

    [Test]
    public async Task EmaDecay_half_life_roundtrip()
    {
        var a = EmaDecay.AlphaFromHalfLife(10);
        await Assert.That(a).IsEqualTo(1.0 - Math.Pow(0.5, 0.1)).Within(1e-12);
        await Assert.That(EmaDecay.HalfLifeFromAlpha(a)).IsEqualTo(10.0).Within(1e-9);
    }

    [Test]
    public async Task OuPlatt_learns_positive_edge_means_over()
    {
        var c = new OuPlattCalibrator(fallbackSharpness: 0.25);
        for (var i = 0; i < OuPlattCalibrator.MinFit; i++)
            c.Observe(modelExp: 3.0, line: 2.5, ouResult: 1); // edge +0.5 → over
        var p = c.OverProb(3.0, 2.5);
        await Assert.That(p).IsGreaterThan(0.55);
    }

    [Test]
    public async Task RidgeLogistic_separates_signed_edge()
    {
        var x = new List<double[]>();
        var y = new List<double>();
        for (var i = 0; i < 40; i++)
        {
            x.Add([1.0, -5.0]);
            y.Add(0.0);
            x.Add([1.0, 5.0]);
            y.Add(1.0);
        }

        var beta = RidgeLogistic.Fit(x, y, lambda: 1.0);
        var pLow = RidgeLogistic.Predict(beta, [1.0, -5.0]);
        var pHigh = RidgeLogistic.Predict(beta, [1.0, 5.0]);
        await Assert.That(pHigh).IsGreaterThan(pLow);
        await Assert.That(pHigh).IsGreaterThan(0.7);
        await Assert.That(pLow).IsLessThan(0.3);
    }
}
