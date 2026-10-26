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
    public async Task Efg_prior_key_and_adj()
    {
        await Assert.That(EfgPriorMath.PriorFileKey(2025)).IsEqualTo("2023_24");
        await Assert.That(EfgPriorMath.AdjPts(0.55, 0.55, 0.54)).IsEqualTo(2.0).Within(1e-9);
    }
}
