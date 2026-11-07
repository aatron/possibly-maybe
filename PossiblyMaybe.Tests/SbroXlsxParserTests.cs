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
    public async Task SeasonYearsFromPath_Nhl2021_IsCovidShortSeason()
    {
        var (y1, y2) = SbroXlsxParser.SeasonYearsFromPath("nhl_odds_2021.xlsx");
        await Assert.That(y1).IsEqualTo(2020);
        await Assert.That(y2).IsEqualTo(2021);
        await Assert.That(SbroXlsxParser.ParseMdd("113", y1, y2)).IsEqualTo(new DateOnly(2021, 1, 13));
    }

    [Test]
    public async Task TryPickNhlOu_AcceptsGoalTotals()
    {
        await Assert.That(SbroXlsxParser.TryPickNhlOu("6", "6", "5.5", "5.5", out var o, out var c)).IsTrue();
        await Assert.That(o).IsEqualTo(6);
        await Assert.That(c).IsEqualTo(5.5);
        await Assert.That(SbroXlsxParser.TryPickNhlOu("NL", "NL", "6", "6", out _, out _)).IsFalse();
        await Assert.That(SbroXlsxParser.TryPickNhlOu("-155", "135", "-123", "103", out _, out _)).IsFalse();
    }

    [Test]
    public async Task ParseNhlFile_ReadsOpenOuJuice_WhenPresent()
    {
        var file = ResolveNhlFile("nhl_odds_2007-08.xlsx");
        if (file is null)
            return; // raw odds are gitignored; skip when absent

        var rows = SbroXlsxParser.ParseNhlFile(file).Take(3).ToList();
        await Assert.That(rows.Count).IsEqualTo(3);
        await Assert.That(rows[0].Away).IsEqualTo("Anaheim");
        await Assert.That(rows[0].Home).IsEqualTo("LosAngeles");
        await Assert.That(rows[0].OpenTotal).IsEqualTo(6);
        await Assert.That(rows[0].CloseTotal).IsEqualTo(6);
        await Assert.That(rows[0].OpenJuiceAssumed).IsFalse();
        await Assert.That(rows[0].CloseJuiceAssumed).IsFalse();
        await Assert.That(SportTeams.TryNhl(rows[0].Away)).IsEqualTo("ANA");
        await Assert.That(OregonExclusion.IsExcluded("NHL", SportTeams.TryNhl(rows[0].Away))).IsFalse();
    }

    static string? ResolveNhlFile(string name)
    {
        var here = AppContext.BaseDirectory;
        for (var dir = new DirectoryInfo(here); dir != null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "data", "raw", "round3_odds", "archive_org", "sbro_nhl", name);
            if (File.Exists(candidate)) return candidate;
        }

        var sibling = Path.GetFullPath(Path.Combine(here, "..", "..", "..", "..", "..", "possibly-maybe",
            "data", "raw", "round3_odds", "archive_org", "sbro_nhl", name));
        return File.Exists(sibling) ? sibling : null;
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

    [Test]
    public async Task NcaaCode_UnmappedNeverAliasesOntoExistingOrOregon()
    {
        var duke = SportTeams.NcaaCode("Duke");
        var gonzaga = SportTeams.NcaaCode("Gonzaga");
        var unknown = SportTeams.NcaaCode("Some New College XYZ");
        await Assert.That(duke).IsEqualTo("duke");
        await Assert.That(gonzaga).IsEqualTo("gonzaga");
        await Assert.That(unknown).IsEqualTo("somenewcollegexy"); // SanitizeNcaa truncates to 16
        await Assert.That(unknown).IsNotEqualTo(duke);
        await Assert.That(unknown).IsNotEqualTo(gonzaga);
        await Assert.That(unknown).IsNotEqualTo("ore");
        await Assert.That(unknown).IsNotEqualTo("orst");
        await Assert.That(unknown).IsNotEqualTo("port");
        await Assert.That(unknown).IsNotEqualTo("up");
        // TryNcaa only hits Oregon cardinal map
        await Assert.That(SportTeams.TryNcaa("Duke")).IsNull();
        await Assert.That(SportTeams.TryNcaa("Oregon Ducks")).IsEqualTo("ore");
        await Assert.That(SportTeams.ResolveNcaa("ORE", "Oregon Ducks", null)).IsEqualTo("ore");
        await Assert.That(SportTeams.ResolveNcaa(null, "Gonzaga Bulldogs", "Gonzaga")).IsEqualTo("gonzagabulldogs");
    }
}
