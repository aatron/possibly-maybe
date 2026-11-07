using System.Text.Json;
using PossiblyMaybe.Cli.Import;
using PossiblyMaybe.Cli.Paper;
using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Tests;

public class ResultsRefreshTests
{
    private static string Event(string state, bool done, string home, string away, string abbrH = "H", string abbrA = "A") => """
        {"events":[{"id":"1","date":"2026-10-31T19:45Z","competitions":[{"status":{"type":{"completed":@DONE,"state":"@STATE"}},
        "competitors":[{"homeAway":"home","score":"2","team":{"abbreviation":"@AH","displayName":"@HOME","shortDisplayName":"@HOME"}},
        {"homeAway":"away","score":"0","team":{"abbreviation":"@AA","displayName":"@AWAY","shortDisplayName":"@AWAY"}}]}]}]}
        """.Replace("@DONE", done ? "true" : "false").Replace("@STATE", state).Replace("@AH", abbrH).Replace("@AA", abbrA)
            .Replace("@HOME", home).Replace("@AWAY", away);

    [Test]
    public async Task ParseFinals_SkipsUnfinished_AndKeepsLeMansDistinctFromMetz()
    {
        using var live = JsonDocument.Parse(Event("in", false, "Troyes", "Le Mans"));
        await Assert.That(ResultsRefresh.ParseFinals(live, "LIGUE1").Count).IsEqualTo(0);

        using var done = JsonDocument.Parse(Event("post", true, "Troyes", "Le Mans", "TRY", "MNS"));
        var g = ResultsRefresh.ParseFinals(done, "LIGUE1").Single();
        await Assert.That(g.Home).IsEqualTo("Troyes");
        await Assert.That(g.Away).IsEqualTo("Le Mans");
        await Assert.That(g.HomeScore).IsEqualTo(2);
    }

    [Test]
    public async Task ParseFinals_DropsAllStarExhibitions()
    {
        using var doc = JsonDocument.Parse(Event("post", true, "MLS All-Stars", "Liga MX All-Stars"));
        await Assert.That(ResultsRefresh.ParseFinals(doc, "MLS").Count).IsEqualTo(0);
    }

    [Test]
    [Arguments("LIGUE1", "MNS", "Le Mans", "Le Mans")]
    [Arguments("LIGUE1", "BRE", "Brest", "Stade Brestois")]
    [Arguments("LIGAMX", "ATL", "Atlante", "Atlante")]
    [Arguments("LIGAMX", "UNAM", "Pumas UNAM", "Pumas U.N.A.M.")]
    [Arguments("LIGAMX", "ASL", "Atlético de San Luis", "Atletico San Luis")]
    [Arguments("SERIEA", "INT", "Internazionale", "Inter Milan")]
    [Arguments("BUNDESLIGA", "MUN", "Bayern Munich", "Bayern Munchen")]
    [Arguments("BUNDESLIGA", "BMG", "Borussia Mönchengladbach", "Borussia Monchengladbach")]
    [Arguments("MLS", "LA", "LA Galaxy", "LAG")]
    [Arguments("MLS", "XXX", "Brand New FC", "Brand New FC")]
    public async Task ResolveSoccer_NamesWinAndNewClubsNeverAlias(string sport, string abbr, string display, string expected)
    {
        await Assert.That(SportTeams.ResolveSoccer(sport, abbr, display, display)).IsEqualTo(expected);
    }

    [Test]
    public async Task KeyDateAndSeason_FollowWarehouseConventions()
    {
        var late = DateTimeOffset.Parse("2026-10-26T01:00:00Z"); // 9pm ET on the 25th
        await Assert.That(ResultsRefresh.KeyDate("NBA", late)).IsEqualTo(new DateOnly(2026, 10, 25));
        await Assert.That(ResultsRefresh.KeyDate("MLS", late)).IsEqualTo(new DateOnly(2026, 10, 26));
        await Assert.That(ResultsRefresh.SeasonFor("NBA", new DateOnly(2026, 10, 25))).IsEqualTo("2027");
        await Assert.That(ResultsRefresh.SeasonFor("LIGUE1", new DateOnly(2026, 10, 31))).IsEqualTo("2026-2027");
        await Assert.That(ResultsRefresh.SeasonFor("SERIEA", new DateOnly(2027, 3, 1))).IsEqualTo("2026-2027");
    }

    [Test]
    public async Task IsStale_FlagsOldOrMissingResults()
    {
        await Assert.That(DailySlate.IsStale(DateTimeOffset.Parse("2026-05-17T19:00:00Z"), "20261107")).IsTrue();
        await Assert.That(DailySlate.IsStale(DateTimeOffset.Parse("2026-10-31T19:00:00Z"), "20261107")).IsFalse();
        await Assert.That(DailySlate.IsStale(null, "20261107")).IsTrue();
    }
}
