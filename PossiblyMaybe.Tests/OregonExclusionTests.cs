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
