using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Tests;

public class OddsMathTests
{
    [Test]
    [Arguments(-150, 1.6666666667)]
    [Arguments(140, 2.4)]
    [Arguments(-110, 1.9090909091)]
    public async Task AmericanToDecimal_KnownQuotes(int american, double expected)
    {
        await Assert.That(OddsMath.AmericanToDecimal(american))
            .IsEqualTo(expected).Within(1e-9);
    }

    [Test]
    [Arguments(-150)]
    [Arguments(140)]
    [Arguments(-110)]
    public async Task DecimalRoundTrip_PreservesAmerican(int american)
    {
        var dec = OddsMath.AmericanToDecimal(american);
        await Assert.That(OddsMath.DecimalToAmerican(dec)).IsEqualTo(american);
    }

    [Test]
    public async Task ImpliedFromAmerican_KnownQuotes()
    {
        await Assert.That(OddsMath.ImpliedProbabilityFromAmerican(-150))
            .IsEqualTo(0.6).Within(1e-12);
        await Assert.That(OddsMath.ImpliedProbabilityFromAmerican(140))
            .IsEqualTo(100.0 / 240.0).Within(1e-12);
        await Assert.That(OddsMath.ImpliedProbabilityFromAmerican(-110))
            .IsEqualTo(110.0 / 210.0).Within(1e-12);
    }

    [Test]
    public async Task NoVig_EvenJuice_IsHalfHalf()
    {
        var (a, b) = OddsMath.NoVigTwoWay(-110, -110);
        await Assert.That(a).IsEqualTo(0.5).Within(1e-12);
        await Assert.That(b).IsEqualTo(0.5).Within(1e-12);
        await Assert.That(a + b).IsEqualTo(1.0).Within(1e-12);
    }

    [Test]
    public async Task NoVig_Minus150Plus140_SumsToOneAndRemovesOverround()
    {
        var rawA = OddsMath.ImpliedProbabilityFromAmerican(-150);
        var rawB = OddsMath.ImpliedProbabilityFromAmerican(140);
        await Assert.That(rawA + rawB).IsGreaterThan(1.0);

        var (fairA, fairB) = OddsMath.NoVigTwoWay(-150, 140);
        await Assert.That(fairA + fairB).IsEqualTo(1.0).Within(1e-12);
        await Assert.That(fairA).IsEqualTo(rawA / (rawA + rawB)).Within(1e-12);
        await Assert.That(fairB).IsEqualTo(rawB / (rawA + rawB)).Within(1e-12);
        await Assert.That(fairA).IsGreaterThan(fairB);
    }

    [Test]
    public async Task ExpectedValue_PositiveWhenProbBeatsPrice()
    {
        // -110 implies ~52.38%; true 60% → positive EV
        var ev = OddsMath.ExpectedValue(0.60, -110);
        await Assert.That(ev).IsGreaterThan(0);
        await Assert.That(ev).IsEqualTo(0.60 * OddsMath.AmericanToDecimal(-110) - 1.0).Within(1e-12);
    }

    [Test]
    public async Task ExpectedValue_NegativeWhenProbBelowPrice()
    {
        var ev = OddsMath.ExpectedValue(0.40, -110);
        await Assert.That(ev).IsLessThan(0);
    }

    [Test]
    public async Task FractionalKelly_QuarterOfFull_AndZeroWithoutEdge()
    {
        var fullish = OddsMath.FractionalKelly(0.60, -110, fraction: 1.0);
        var quarter = OddsMath.FractionalKelly(0.60, -110, fraction: 0.25);
        await Assert.That(quarter).IsEqualTo(fullish * 0.25).Within(1e-12);
        await Assert.That(quarter).IsGreaterThan(0);

        await Assert.That(OddsMath.FractionalKelly(0.40, -110, fraction: 0.25))
            .IsEqualTo(0.0);
    }

    [Test]
    [Arguments(0.20, 0.2112)]
    [Arguments(0.50, 0.5175)]
    [Arguments(0.80, 0.8112)]
    public async Task EffectiveBuyPrice_KalshiMatchesVenueTable(double p, double allIn)
    {
        await Assert.That(OddsMath.EffectiveBuyPrice(p, PaperVenues.Kalshi))
            .IsEqualTo(allIn).Within(1e-9);
        await Assert.That(OddsMath.EffectiveBuyPrice(p, PaperVenues.CryptoCom))
            .IsEqualTo(allIn).Within(1e-9);
    }

    [Test]
    [Arguments(0.20, 0.22)]
    [Arguments(0.50, 0.52)]
    [Arguments(0.80, 0.82)]
    public async Task EffectiveBuyPrice_RobinhoodNoGoldPlusExchange(double p, double allIn)
    {
        await Assert.That(OddsMath.EffectiveBuyPrice(p, PaperVenues.Robinhood))
            .IsEqualTo(allIn).Within(1e-9);
    }

    [Test]
    public async Task EffectiveBuyPrice_DraftKingsUnchanged()
    {
        await Assert.That(OddsMath.EffectiveBuyPrice(0.50, PaperVenues.DraftKings))
            .IsEqualTo(0.50).Within(1e-12);
    }

    [Test]
    public async Task ExpectedValue_FeeAdjustedIsStrictlyWorseNearEven()
    {
        // p=0.53: fee-blind EV@50¢ = +6% (GO); all-in 51.75¢ EV ≈ +2.4% (SKIP under 4% gate)
        const double pModel = 0.53;
        const double ask = 0.50;
        var blindAm = OddsMath.AmericanFromBuyPrice(ask);
        var feeAm = OddsMath.AmericanFromBuyPrice(OddsMath.EffectiveBuyPrice(ask, PaperVenues.Kalshi));
        var blindEv = OddsMath.ExpectedValue(pModel, blindAm);
        var feeEv = OddsMath.ExpectedValue(pModel, feeAm);
        await Assert.That(blindEv).IsGreaterThan(0.04);
        await Assert.That(feeEv).IsLessThan(blindEv);
        await Assert.That(feeEv).IsLessThan(0.04);
    }
}
