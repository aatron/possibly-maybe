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

    [Test]
    public async Task Totals_LearnsHighTotalCrew()
    {
        var fx = new RefEffects();
        for (var i = 0; i < 30; i++)
            fx.ObserveTotal(["Scott Foster"], totalPts: 240, excludeFromFit: false, possessions: 105);
        for (var i = 0; i < 30; i++)
            fx.ObserveTotal(["Quiet Crew"], totalPts: 200, excludeFromFit: false, possessions: 95);

        await Assert.That(fx.CrewTotalsBias(["Scott Foster"])).IsGreaterThan(5);
        await Assert.That(fx.CrewPaceBias(["Scott Foster"])).IsGreaterThan(1);
        await Assert.That(fx.CrewFoulPaceResidual(["Scott Foster"])).IsGreaterThan(5);
    }

    [Test]
    public async Task Totals_Donaghy_ExcludedFromFit()
    {
        var fx = new RefEffects();
        for (var i = 0; i < 40; i++)
            fx.ObserveTotal(["Tim Donaghy"], totalPts: 260, excludeFromFit: true, possessions: 110);
        await Assert.That(fx.CrewTotalsBias(["Tim Donaghy"])).IsEqualTo(0.0);
        await Assert.That(fx.CrewFoulPaceResidual(["Tim Donaghy"])).IsEqualTo(0.0);
    }
}
