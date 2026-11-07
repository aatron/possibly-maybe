using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Tests;

public class OregonExclusionTests
{
    [Test]
    [Arguments("NBA", "POR")]
    [Arguments("NBA", "Portland Trail Blazers")]
    [Arguments("NBA", "Trail Blazers")]
    [Arguments("NBA", "portland")]
    [Arguments("MLS", "Portland Timbers")]
    [Arguments("MLS", "POR")]
    [Arguments("NCAAF", "Oregon Ducks")]
    [Arguments("NCAAF", "Oregon State")]
    [Arguments("NCAAB", "Oregon St.")]
    [Arguments("NCAAB", "Oregon State Beavers")]
    [Arguments("NCAAF", "ORST")]
    [Arguments("NCAAF", "Portland State Vikings")]
    [Arguments("NWSL", "Portland Thorns FC")]
    [Arguments("NBA", "  PORTLAND  ")]
    // SBRO CamelCase / concatenated spellings
    [Arguments("NCAAB", "OregonState")]
    [Arguments("NCAAB", "PortlandState")]
    [Arguments("NCAAF", "PortlandSt")]
    [Arguments("NCAAB", "PortlandU")]
    [Arguments("NCAAB", "Portland U")]
    [Arguments("NCAAB", "University of Portland")]
    [Arguments("NCAAF", "Portland Pilots")]
    [Arguments("NCAAB", "Pilots")]
    [Arguments("NCAAB", "PORT")]
    [Arguments("NCAAB", "UP")]
    [Arguments("NCAAB", "PORTLANDU")]
    [Arguments("NCAAF", "University of Oregon")]
    [Arguments("NCAAB", "Oregon State University")]
    [Arguments("NCAAB", "Portland State University")]
    [Arguments("NCAAB", "U of Portland")]
    [Arguments("WNBA", "Portland Fire")]
    [Arguments("WNBA", "POR")]
    [Arguments(null, "Portland Winterhawks")]
    [Arguments("NHL", "Portland Winterhawks")]
    [Arguments("NHL", "winterhawks")]
    [Arguments(null, "Hillsboro Hops")]
    [Arguments(null, "Eugene Emeralds")]
    public async Task Excluded(string sport, string team)
        => await Assert.That(OregonExclusion.IsExcluded(sport, team)).IsTrue();

    [Test]
    [Arguments("NCAAF", "Ohio State")]
    [Arguments("NCAAF", "Oklahoma State")]
    [Arguments("NCAAF", "OSU")]
    [Arguments("NBA", "Portland Sea Dogs")]
    [Arguments("NBA", "BOS")]
    [Arguments("MLS", "Seattle Sounders")]
    [Arguments("NBA", "ORE")]
    // psu = Penn State in ESPN/GetSTAM; must not exclude after code fix
    [Arguments("NCAAF", "PSU")]
    [Arguments("NCAAF", "Penn State")]
    [Arguments("MLS", "Chicago Fire")]
    [Arguments("MLS", "Fire")]
    [Arguments("NFL", "Minnesota Vikings")]
    [Arguments("NFL", "Vikings")]
    public async Task NotExcluded(string sport, string team)
        => await Assert.That(OregonExclusion.IsExcluded(sport, team)).IsFalse();

    [Test]
    public async Task GameKey_EitherSide()
    {
        await Assert.That(OregonExclusion.IsExcludedGameKey("MLS", "20261101 HOU@POR")).IsTrue();
        await Assert.That(OregonExclusion.IsExcludedGameKey("NBA", "20261108 POR@LAL")).IsTrue();
        await Assert.That(OregonExclusion.IsExcludedGameKey(null, "20261108 ORST@UCLA")).IsTrue();
        await Assert.That(OregonExclusion.IsExcludedGameKey("NBA", "20261108 BOS@LAL")).IsFalse();
    }
}
