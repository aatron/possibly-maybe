using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Tests;

public class RefEffectsTests
{
    [Test]
    public async Task CrewBias_LearnsHomeHappyRef()
    {
        var fx = new RefEffects();
        for (var i = 0; i < 30; i++)
            fx.Observe(["Tony Brothers"], homeWon: true, excludeFromFit: false);
        for (var i = 0; i < 30; i++)
            fx.Observe(["Other Ref"], homeWon: false, excludeFromFit: false);

        await Assert.That(fx.CrewHomeBias(["Tony Brothers"])).IsGreaterThan(0.05);
    }

    [Test]
    public async Task Donaghy_ExcludedFromFit()
    {
        var fx = new RefEffects();
        for (var i = 0; i < 40; i++)
            fx.Observe(["Tim Donaghy"], homeWon: true, excludeFromFit: true);
        await Assert.That(fx.CrewHomeBias(["Tim Donaghy"])).IsEqualTo(0.0);
    }
}
