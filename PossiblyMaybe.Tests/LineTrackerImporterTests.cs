using PossiblyMaybe.Cli.Import;

namespace PossiblyMaybe.Tests;

public class LineTrackerImporterTests
{
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
