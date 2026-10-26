using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Tests;

public class TeamCodesTests
{
    [Test]
    [Arguments("sa", "SAS")]
    [Arguments("gs", "GSW")]
    [Arguments("LA Lakers", "LAL")]
    [Arguments("Golden State", "GSW")]
    [Arguments("por", "POR")]
    public async Task Normalize_KnownTokens(string raw, string expected)
    {
        await Assert.That(TeamCodes.Normalize(raw)).IsEqualTo(expected);
    }

    [Test]
    public async Task IsDonaghy_FlagsTim()
    {
        await Assert.That(TeamCodes.IsDonaghy("Tim Donaghy")).IsTrue();
        await Assert.That(TeamCodes.IsDonaghy("James Capers")).IsFalse();
    }

    [Test]
    public async Task GameKey_Stable()
    {
        await Assert.That(TeamCodes.GameKey(new DateOnly(2024, 1, 15), "BOS", "NYK"))
            .IsEqualTo("nba:2024-01-15:BOS:NYK");
    }
}
