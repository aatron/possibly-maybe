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
    [Arguments("NCAAF", "Oregon")]
    [Arguments("NCAAF", "Oregon State")]
    [Arguments("NCAAF", "OregonState")]
    [Arguments("NCAAF", "Oregon St")]
    [Arguments("NCAAF", "Oregon St.")]
    [Arguments("NCAAF", "OregonSt")]
    [Arguments("NCAAF", "Oregon State Beavers")]
    [Arguments("NCAAF", "Beavers")]
    [Arguments("NCAAF", "ORE")]
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
    [Arguments("NCAAF", "Oregon State University")]
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
    public async Task Ncaaf_SbroCodes_MapToExclusion()
    {
        // SBRO football spellings → NcaaCode → OregonExclusion
        await Assert.That(SportTeams.NcaaCode("Oregon")).IsEqualTo("ore");
        await Assert.That(SportTeams.NcaaCode("OregonState")).IsEqualTo("orst");
        await Assert.That(SportTeams.NcaaCode("PortlandSt")).IsEqualTo("port");
        await Assert.That(OregonExclusion.IsExcluded("NCAAF", SportTeams.NcaaCode("Oregon"))).IsTrue();
        await Assert.That(OregonExclusion.IsExcluded("NCAAF", SportTeams.NcaaCode("OregonState"))).IsTrue();
        await Assert.That(OregonExclusion.IsExcluded("NCAAF", SportTeams.NcaaCode("PortlandSt"))).IsTrue();
        await Assert.That(OregonExclusion.IsExcludedGameKey("NCAAF", "20211113 ore@stanford")).IsTrue();
        await Assert.That(OregonExclusion.IsExcludedGameKey("NCAAF", "20211113 orst@ucla")).IsTrue();
    }

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
    // T2a: SBRO NHL says "Anaheim" (not "Ducks"); Anaheim Ducks are not Oregon
    [Arguments("NHL", "Anaheim")]
    [Arguments("NHL", "Anaheim Ducks")]
    [Arguments("NHL", "ANA")]
    public async Task NotExcluded(string sport, string team)
        => await Assert.That(OregonExclusion.IsExcluded(sport, team)).IsFalse();

    [Test]
    public async Task Nhl_AnaheimNotOregon_WhileOregonNamesStillExcluded()
    {
        await Assert.That(SportTeams.TryNhl("Anaheim")).IsEqualTo("ANA");
        await Assert.That(OregonExclusion.IsExcluded("NHL", "Anaheim")).IsFalse();
        await Assert.That(OregonExclusion.IsExcluded("NHL", "Anaheim Ducks")).IsFalse();
        await Assert.That(OregonExclusion.IsExcluded("NHL", "ANA")).IsFalse();
        await Assert.That(OregonExclusion.IsExcluded("NHL", SportTeams.TryNhl("Anaheim"), "BOS")).IsFalse();

        // Cardinal Oregon names still excluded under NHL sport id
        await Assert.That(OregonExclusion.IsExcluded("NHL", "Oregon Ducks")).IsTrue();
        await Assert.That(OregonExclusion.IsExcluded("NHL", "Oregon")).IsTrue();
        await Assert.That(OregonExclusion.IsExcluded("NHL", "Portland Winterhawks")).IsTrue();
        await Assert.That(OregonExclusion.IsExcluded("NHL", "Portland")).IsTrue();
        await Assert.That(OregonExclusion.IsExcluded("NHL", "Beavers")).IsTrue();
    }

    [Test]
    public async Task GameKey_EitherSide()
    {
        await Assert.That(OregonExclusion.IsExcludedGameKey("MLS", "20261101 HOU@POR")).IsTrue();
        await Assert.That(OregonExclusion.IsExcludedGameKey("NBA", "20261108 POR@LAL")).IsTrue();
        await Assert.That(OregonExclusion.IsExcludedGameKey(null, "20261108 ORST@UCLA")).IsTrue();
        await Assert.That(OregonExclusion.IsExcludedGameKey("NBA", "20261108 BOS@LAL")).IsFalse();
    }
}
