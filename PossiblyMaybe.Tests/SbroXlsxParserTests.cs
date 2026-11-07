using PossiblyMaybe.Cli.Import;
using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Tests;

public class SbroXlsxParserTests
{
    [Test]
    public async Task ParseMdd_SepAndJan_UseSeasonYears()
    {
        await Assert.That(SbroXlsxParser.ParseMdd("906", 2007, 2008)).IsEqualTo(new DateOnly(2007, 9, 6));
        await Assert.That(SbroXlsxParser.ParseMdd("101", 2007, 2008)).IsEqualTo(new DateOnly(2008, 1, 1));
        await Assert.That(SbroXlsxParser.ParseMdd("1105", 2007, 2008)).IsEqualTo(new DateOnly(2007, 11, 5));
        await Assert.That(SbroXlsxParser.ParseMdd("1018", 2022, 2023)).IsEqualTo(new DateOnly(2022, 10, 18));
    }

    [Test]
    public async Task SeasonYearsFromPath_HandlesYyRoll()
    {
        var (y1, y2) = SbroXlsxParser.SeasonYearsFromPath("ncaa_basketball_2007-08.xlsx");
        await Assert.That(y1).IsEqualTo(2007);
        await Assert.That(y2).IsEqualTo(2008);
    }

    [Test]
    public async Task TryPickTotals_TakesLargerSide()
    {
        await Assert.That(SbroXlsxParser.TryPickTotals("50.5", "52.5", "5.5", "5.5", out var o, out var c)).IsTrue();
        await Assert.That(o).IsEqualTo(50.5);
        await Assert.That(c).IsEqualTo(52.5);

        await Assert.That(SbroXlsxParser.TryPickTotals("pk", "37.5", "38.5", "3", out o, out c)).IsTrue();
        await Assert.That(o).IsEqualTo(38.5);
        await Assert.That(c).IsEqualTo(37.5);

        await Assert.That(SbroXlsxParser.TryPickTotals("NL", "NL", "NL", "NL", out _, out _)).IsFalse();
        await Assert.That(SbroXlsxParser.TryPickTotals("4", "3", "5", "2.5", out _, out _)).IsFalse();
    }

    [Test]
    public async Task NcaaCode_OregonStable_AndSanitizeUnknown()
    {
        await Assert.That(SportTeams.NcaaCode("OregonState")).IsEqualTo("orst");
        await Assert.That(SportTeams.NcaaCode("Portland U")).IsEqualTo("up");
        await Assert.That(SportTeams.NcaaCode("PortlandState")).IsEqualTo("port");
        await Assert.That(SportTeams.NcaaCode("Duke")).IsEqualTo("duke");
        await Assert.That(OregonExclusion.IsExcluded("NCAAB", SportTeams.NcaaCode("OregonState"))).IsTrue();
        await Assert.That(OregonExclusion.IsExcluded("NCAAB", SportTeams.NcaaCode("PortlandU"))).IsTrue();
    }
}
