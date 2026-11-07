using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Tests;

public class GuardsTests
{
    [Test]
    public async Task ExceedsEdgeCap_StrictGreaterThan()
    {
        await Assert.That(Guards.ExceedsEdgeCap(0.30, 0.30)).IsFalse();
        await Assert.That(Guards.ExceedsEdgeCap(0.31, 0.30)).IsTrue();
        await Assert.That(Guards.ExceedsEdgeCap(0.20, 0.30)).IsFalse();
    }

    [Test]
    public async Task LineMovedAgainst_Over_WhenLineDrops()
    {
        await Assert.That(Guards.LineMovedAgainst("over", 220.0, 218.0, 1.5)).IsTrue();
        await Assert.That(Guards.LineMovedAgainst("over", 220.0, 219.0, 1.5)).IsFalse();
        await Assert.That(Guards.LineMovedAgainst("over", 220.0, 221.0, 1.5)).IsFalse();
    }

    [Test]
    public async Task LineMovedAgainst_Under_WhenLineRises()
    {
        await Assert.That(Guards.LineMovedAgainst("under", 220.0, 222.0, 1.5)).IsTrue();
        await Assert.That(Guards.LineMovedAgainst("under", 220.0, 221.0, 1.5)).IsFalse();
        await Assert.That(Guards.LineMovedAgainst("under", null, 222.0, 1.5)).IsFalse();
    }

    [Test]
    public async Task StrikeMovedAgainst_WorseningJuice()
    {
        // -110 → ~0.524; -150 → 0.60; delta ≈ 0.076 < 0.08
        await Assert.That(Guards.StrikeMovedAgainst(-110, -150, 0.08)).IsFalse();
        // -110 → -200: 0.524 → 0.667 ≈ 0.143
        await Assert.That(Guards.StrikeMovedAgainst(-110, -200, 0.08)).IsTrue();
        await Assert.That(Guards.StrikeMovedAgainst(null, -200, 0.08)).IsFalse();
    }

    [Test]
    public async Task SoccerMovedAgainst_LineOrStrike()
    {
        await Assert.That(Guards.SoccerMovedAgainst("over", 2.5, 2.0, 0.5, -110, -110)).IsTrue();
        await Assert.That(Guards.SoccerMovedAgainst("over", 2.5, 2.25, 0.5, -110, -110)).IsFalse();
        await Assert.That(Guards.SoccerMovedAgainst("under", 2.5, 2.5, 0.5, -110, -200)).IsTrue();
    }
}
