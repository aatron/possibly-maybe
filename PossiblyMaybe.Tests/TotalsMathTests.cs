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
    public async Task NormalOverProb_matches_phi_at_6pt_edge()
    {
        // logistic sharpness 0.25 → ~0.82; Φ(6/22.8) ≈ 0.604
        var logistic = TotalsMath.OverProb(226, 220, sharpness: 0.25);
        var normal = TotalsMath.NormalOverProb(226, 220, sigma: 22.8);
        await Assert.That(logistic).IsGreaterThan(0.80);
        await Assert.That(normal).IsEqualTo(0.6038).Within(0.005);
        await Assert.That(TotalsMath.NormalOverProb(220, 220, 22.8)).IsEqualTo(0.5).Within(1e-9);
        await Assert.That(TotalsMath.NormalCdf(0)).IsEqualTo(0.5).Within(1e-9);
    }

    [Test]
    public async Task ExpandingResidualSigma_uses_fallback_then_rmse()
    {
        var s = new ExpandingResidualSigma();
        await Assert.That(s.Sigma).IsEqualTo(ExpandingResidualSigma.FallbackSigma);
        for (var i = 0; i < ExpandingResidualSigma.MinN; i++)
            s.Observe(10.0);
        await Assert.That(s.Sigma).IsEqualTo(10.0).Within(1e-9);
    }

    [Test]
    public async Task PoissonTotalOverProb_half_line_and_symmetry()
    {
        // μ=2.5 → P(T>2.5)=P(T≥3)=1−(p0+p1+p2); independent λs sum
        var p = TotalsMath.PoissonTotalOverProb(1.25, 1.25, 2.5);
        var mu = 2.5;
        var p0 = Math.Exp(-mu);
        var p1 = p0 * mu;
        var p2 = p1 * mu / 2;
        var expect = 1 - (p0 + p1 + p2);
        await Assert.That(p).IsEqualTo(expect).Within(1e-12);
        await Assert.That(p).IsGreaterThan(0.4);
        await Assert.That(p).IsLessThan(0.6);
        // High μ ⇒ over heavily favored at low line
        var pHigh = TotalsMath.PoissonTotalOverProb(3.0, 3.0, 2.5);
        await Assert.That(pHigh).IsGreaterThan(0.9);
        // Integer line: P(T>3)=1−F(3) at μ=3
        var pInt = TotalsMath.PoissonTotalOverProb(1.5, 1.5, 3.0);
        var m3 = 3.0;
        var q0 = Math.Exp(-m3);
        var q1 = q0 * m3;
        var q2 = q1 * m3 / 2;
        var q3 = q2 * m3 / 3;
        await Assert.That(pInt).IsEqualTo(1 - (q0 + q1 + q2 + q3)).Within(1e-12);
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

    [Test]
    public async Task MarketBlend_inverse_variance_matches_weighted_mean()
    {
        // σ_m=2, σ_ℓ=1 → w_line = σ_m²/(σ_m²+σ_ℓ²) = 4/5 = 0.8
        await Assert.That(MarketBlend.MarketWeightFromSigmas(2, 1)).IsEqualTo(0.8).Within(1e-12);
        await Assert.That(MarketBlend.MarketWeightFromRatio(2)).IsEqualTo(0.8).Within(1e-12);
        var exp = MarketBlend.InverseVariance(model: 100, line: 220, sigmaModel: 2, sigmaLine: 1);
        await Assert.That(exp).IsEqualTo(0.2 * 100 + 0.8 * 220).Within(1e-9);
        var (sm, sl) = MarketBlend.FromResidualMoments(sumSqModelErr: 400, sumSqLineErr: 100, n: 100);
        await Assert.That(sm).IsEqualTo(2.0).Within(1e-12);
        await Assert.That(sl).IsEqualTo(1.0).Within(1e-12);
    }
}
