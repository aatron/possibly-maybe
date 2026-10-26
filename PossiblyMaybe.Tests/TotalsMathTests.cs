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
}
