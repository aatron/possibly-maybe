using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Tests;

public class StrikeMatchTests
{
    [Test]
    public async Task MaxDelta_NbaVsSoccer()
    {
        await Assert.That(StrikeMatch.MaxDelta("NBA")).IsEqualTo(0.5);
        await Assert.That(StrikeMatch.MaxDelta("MLS")).IsEqualTo(0.25);
        await Assert.That(StrikeMatch.MaxDelta("nba")).IsEqualTo(0.5);
    }

    [Test]
    [Arguments("NBA", 229.5, 229.5, true)]
    [Arguments("NBA", 230.0, 229.5, true)]
    [Arguments("NBA", 230.5, 229.5, false)]
    [Arguments("MLS", 2.5, 2.5, true)]
    [Arguments("MLS", 2.75, 2.5, true)]
    [Arguments("MLS", 3.0, 2.5, false)]
    public async Task WithinMaxDelta_Boundaries(string sport, double strike, double line, bool ok)
    {
        await Assert.That(StrikeMatch.WithinMaxDelta(sport, strike, line)).IsEqualTo(ok);
    }
}
