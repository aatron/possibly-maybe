using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Tests;

public class IngestHelpersTests
{
    [Test]
    public async Task Donaghy_And_GameKey_ForIngestJoin()
    {
        await Assert.That(TeamCodes.IsDonaghy("TIM DONAGHY")).IsTrue();
        await Assert.That(TeamCodes.GameKey(new DateOnly(2007, 10, 30), "SAS", "POR"))
            .IsEqualTo("nba:2007-10-30:SAS:POR");
        await Assert.That(TeamCodes.Normalize("por")).IsEqualTo("POR");
    }
}
