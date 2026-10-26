using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Tests;

public class EloClvTests
{
    [Test]
    public async Task Elo_HomeFavorite_AboveHalf()
    {
        var elo = new EloBook();
        elo.Update("BOS", "DET", homeWon: true);
        elo.Update("BOS", "DET", homeWon: true);
        await Assert.That(elo.HomeWinProbability("BOS", "DET")).IsGreaterThan(0.5);
    }

    [Test]
    public async Task Clv_EqualPrices_IsZero()
    {
        await Assert.That(Clv.ProbabilityClv(-110, -110)).IsEqualTo(0.0).Within(1e-12);
    }

    [Test]
    public async Task Clv_longerDogPrice_HasLowerImpliedThanClose()
    {
        // Bet +150 vs close +100 → lower implied win prob at bet time than close.
        await Assert.That(Clv.ProbabilityClv(150, 100)).IsLessThan(0);
    }
}
