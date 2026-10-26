using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Tests;

public class SpreadMathTests
{
    [Test]
    public async Task HomeAts_FavoriteCovers()
    {
        // Home -5, wins 110-100 → margin 10, 10-5>0 cover
        await Assert.That(SpreadMath.HomeAtsResult(110, 100, -5)).IsEqualTo(1);
        await Assert.That(SpreadMath.HomeAtsResult(104, 100, -5)).IsEqualTo(-1);
        await Assert.That(SpreadMath.HomeAtsResult(105, 100, -5)).IsEqualTo(0);
    }

    [Test]
    public async Task SpreadClv_BetterHomeNumber_IsPositive()
    {
        await Assert.That(SpreadMath.HomeSpreadClvPoints(-3, -5)).IsEqualTo(2.0);
    }
}
