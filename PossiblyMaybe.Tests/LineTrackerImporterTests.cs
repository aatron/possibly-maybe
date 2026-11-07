using PossiblyMaybe.Cli.Import;
using PossiblyMaybe.Data;

namespace PossiblyMaybe.Tests;

public class LineTrackerImporterTests
{
    [Test]
    public async Task ResolveTeam_MapsKnownCodes_KeepsUnknown()
    {
        await Assert.That(LineTrackerImporter.ResolveTeam("NFL", "WSH")).IsEqualTo("WAS");
        await Assert.That(LineTrackerImporter.ResolveTeam("NBA", "GS")).IsEqualTo("GSW");
        await Assert.That(LineTrackerImporter.ResolveTeam("MLS", "LAFC")).IsEqualTo("LAFC");
        await Assert.That(LineTrackerImporter.ResolveTeam("LIGUE1", "AUX")).IsEqualTo("AJ Auxerre");
        // unmapped keeps own token — never aliases onto another club
        await Assert.That(LineTrackerImporter.ResolveTeam("LALIGA", "XYZFC")).IsEqualTo("XYZFC");
        await Assert.That(LineTrackerImporter.ResolveTeam("NHL", "TB")).IsEqualTo("TB");
        await Assert.That(LineTrackerImporter.ResolveTeam("NHL", "TBL")).IsEqualTo("TB");
        await Assert.That(LineTrackerImporter.ResolveTeam("NHL", "ZZZ")).IsEqualTo("ZZZ");
    }

    [Test]
    public async Task FindGame_MatchesExisting_DoesNotInvent()
    {
        var tip = DateTimeOffset.Parse("2026-11-08T18:00:00Z");
        var games = new List<Game>
        {
            new()
            {
                Id = 42, Sport = "NFL", HomeTeam = "IND", AwayTeam = "DAL",
                TipoffUtc = tip, SourceKey = "nfl:2026-11-08:IND:DAL", Season = "2026",
            },
        };
        var hit = LineTrackerImporter.FindGame(games, "NFL", "IND", "DAL", tip);
        await Assert.That(hit?.Id).IsEqualTo(42);
        var miss = LineTrackerImporter.FindGame(games, "NFL", "KC", "BUF", tip);
        await Assert.That(miss).IsNull();
    }

    [Test]
    public async Task ResolveOpenClose_EspnPrefersReportedOpenLine()
    {
        var t0 = DateTimeOffset.Parse("2026-11-07T12:00:00Z");
        var t1 = DateTimeOffset.Parse("2026-11-08T17:00:00Z");
        var rows = new[]
        {
            new LineTrackerImporter.SnapRow("NFL", "20261108 DAL@IND 1300", t1.AddHours(1), t0, 1800,
                "espn", "Draft Kings", 51.5, 0.52, 0.52, -110, -110, 52.5),
            new LineTrackerImporter.SnapRow("NFL", "20261108 DAL@IND 1300", t1.AddHours(1), t1, 60,
                "espn", "Draft Kings", 50.5, 0.52, 0.52, -110, -110, 52.5),
        };
        var oc = LineTrackerImporter.ResolveOpenClose(rows, preferEspnOpenLine: true);
        await Assert.That(oc.OpenLine).IsEqualTo(52.5);
        await Assert.That(oc.CloseLine).IsEqualTo(50.5);
        await Assert.That(oc.OpenAt).IsEqualTo(t0);
        await Assert.That(oc.CloseAt).IsEqualTo(t1);
    }

    [Test]
    public async Task ResolveOpenClose_SkipsPostStartForClose()
    {
        var t0 = DateTimeOffset.Parse("2026-11-07T12:00:00Z");
        var started = DateTimeOffset.Parse("2026-11-08T19:00:00Z");
        var rows = new[]
        {
            new LineTrackerImporter.SnapRow("NFL", "k", started, t0, 100, "espn", "DK", 44.5, null, null, -110, -110, null),
            new LineTrackerImporter.SnapRow("NFL", "k", started, started, -5, "espn", "DK", 45.5, null, null, -110, -110, null),
        };
        var oc = LineTrackerImporter.ResolveOpenClose(rows, preferEspnOpenLine: false);
        await Assert.That(oc.OpenLine).IsEqualTo(44.5);
        await Assert.That(oc.CloseLine).IsEqualTo(44.5);
    }

    [Test]
    public async Task PickQuote_InterpolatesExchangeLadder()
    {
        var rows = new[]
        {
            new LineTrackerImporter.SnapRow("NFL", "k", default, default, 60, "kalshi", null, 44.5, 0.62, 0.40, null, null, null),
            new LineTrackerImporter.SnapRow("NFL", "k", default, default, 60, "kalshi", null, 45.5, 0.47, 0.55, null, null, null),
        };
        var q = LineTrackerImporter.PickQuote(rows);
        await Assert.That(q).IsNotNull();
        await Assert.That(q!.Value.Line).IsEqualTo(45.23);
    }
}
