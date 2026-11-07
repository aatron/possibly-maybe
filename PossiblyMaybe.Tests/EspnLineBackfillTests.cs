using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PossiblyMaybe.Cli.Import;
using PossiblyMaybe.Cli.Replay;
using PossiblyMaybe.Data;
using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Tests;

public class EspnLineBackfillTests
{
    [Test]
    public async Task ParseOddsDetail_CoreOpenClose_WithRealJuice()
    {
        const string json = """
            {
              "provider": { "name": "Draft Kings" },
              "overUnder": 221.5,
              "overOdds": -112.0,
              "underOdds": -108.0,
              "open": {
                "over": { "american": "-110" },
                "under": { "american": "-110" },
                "total": { "alternateDisplayValue": "222.5", "american": "222.5" }
              },
              "close": {
                "over": { "american": "-112" },
                "under": { "american": "-108" },
                "total": { "alternateDisplayValue": "221.5", "american": "221.5" }
              }
            }
            """;
        using var doc = JsonDocument.Parse(json);
        var p = EspnLineBackfill.ParseOddsDetail(doc.RootElement);
        await Assert.That(p).IsNotNull();
        await Assert.That(p!.Value.OpenLine).IsEqualTo(222.5);
        await Assert.That(p.Value.CloseLine).IsEqualTo(221.5);
        await Assert.That(p.Value.OpenAmerican).IsEqualTo(-110);
        await Assert.That(p.Value.CloseAmerican).IsEqualTo(-112);
        await Assert.That(p.Value.Provider).IsEqualTo("Draft Kings");
    }

    [Test]
    public async Task ParseOddsDetail_PickcenterNested_AndSingleLineIsCloseOnly()
    {
        const string both = """
            {
              "provider": { "name": "DraftKings" },
              "overUnder": 3.5,
              "overOdds": 110.0,
              "total": {
                "over": {
                  "open": { "line": "o3.5", "odds": "+115" },
                  "close": { "line": "o3.5", "odds": "+110" }
                }
              }
            }
            """;
        using var docBoth = JsonDocument.Parse(both);
        var bothP = EspnLineBackfill.ParseOddsDetail(docBoth.RootElement);
        await Assert.That(bothP!.Value.OpenLine).IsEqualTo(3.5);
        await Assert.That(bothP.Value.CloseLine).IsEqualTo(3.5);
        await Assert.That(bothP.Value.OpenAmerican).IsEqualTo(115);
        await Assert.That(bothP.Value.CloseAmerican).IsEqualTo(110);

        const string onlyOu = """
            { "provider": { "name": "Draft Kings" }, "overUnder": 47.5, "overOdds": -110 }
            """;
        using var docOne = JsonDocument.Parse(onlyOu);
        var one = EspnLineBackfill.ParseOddsDetail(docOne.RootElement);
        await Assert.That(one!.Value.OpenLine).IsNull();
        await Assert.That(one.Value.CloseLine).IsEqualTo(47.5);
        await Assert.That(one.Value.CloseAmerican).IsEqualTo(-110);
    }

    [Test]
    public async Task AsOfView_AtDecisionTime_CannotSeeCloseRow()
    {
        var tipoff = DateTimeOffset.Parse("2026-11-01T23:00:00Z");
        var decision = PointInTime.SlateAsOf("20261101"); // UTC midnight — before tipoff
        await Assert.That(decision < tipoff).IsTrue();

        var openAt = tipoff.AddDays(-7);
        var closeAt = tipoff; // PLAN: close CapturedAt = tipoff

        var open = new OddsAsOfRow(1, 10, "DraftKings", "TOTAL", "over", -110, 222.5, openAt);
        var close = new OddsAsOfRow(2, 10, "DraftKings", "TOTAL", "over", -112, 221.5, closeAt);

        var view = new AsOfView(decision, odds: [open, close]);
        var ids = view.OddsSnapshots(10).Select(o => o.Id).ToArray();
        await Assert.That(ids).IsEquivalentTo(new[] { 1 });
        await Assert.That(view.OddsSnapshots(10).Any(o => o.Id == 2)).IsFalse();
        await Assert.That(EspnLineBackfill.ReplayShouldSkip(view.OddsSnapshots(10))).IsFalse();
    }

    [Test]
    public async Task AsOfView_DbFactory_HidesClose_AndReplaySkipsCloseOnly()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pm-t3-{Guid.NewGuid():N}.db");
        try
        {
            await AppDbContext.EnsureCreatedAsync($"Data Source={path}");
            await using var db = new AppDbContext(
                new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={path}").Options);

            var tipoff = DateTimeOffset.Parse("2026-11-01T23:00:00Z");
            db.Games.Add(new Game
            {
                Sport = "NBA", Season = "2027", SourceKey = "nba:20261101:BOS:MIA",
                TipoffUtc = tipoff, HomeTeam = "BOS", AwayTeam = "MIA",
                HomeScore = 110, AwayScore = 100,
            });
            await db.SaveChangesAsync();
            var gid = (await db.Games.SingleAsync()).Id;

            db.OddsSnapshots.AddRange(
                new OddsSnapshot
                {
                    GameId = gid, Book = Book.DraftKings, Market = "TOTAL", Side = "over",
                    Line = 220, AmericanOdds = -110, PriceKind = PriceKind.Open,
                    ClvQuality = ClvQuality.OpenClose, AssumedJuice = false,
                    CapturedAt = tipoff.AddDays(-7),
                },
                new OddsSnapshot
                {
                    GameId = gid, Book = Book.DraftKings, Market = "TOTAL", Side = "over",
                    Line = 221.5, AmericanOdds = -112, PriceKind = PriceKind.Close,
                    ClvQuality = ClvQuality.OpenClose, AssumedJuice = false,
                    CapturedAt = tipoff,
                });
            await db.SaveChangesAsync();

            var decision = PointInTime.SlateAsOf("20261101");
            var view = await AsOfViewFactory.FromDbAsync(db, decision, sport: "NBA");
            var odds = view.OddsSnapshots(gid);
            await Assert.That(odds.Count).IsEqualTo(1);
            await Assert.That(odds[0].Line).IsEqualTo(220);
            await Assert.That(odds[0].CapturedAt).IsEqualTo(tipoff.AddDays(-7));

            // Close-only game → replay skips rather than betting.
            var closeOnly = new[]
            {
                new OddsSnapshot
                {
                    GameId = 99, Book = Book.DraftKings, Market = "TOTAL", Side = "over",
                    Line = 47.5, AmericanOdds = -110, PriceKind = PriceKind.Close,
                    ClvQuality = ClvQuality.CloseOnly, AssumedJuice = true,
                    CapturedAt = tipoff,
                },
            };
            await Assert.That(EspnLineBackfill.ReplayShouldSkip(closeOnly)).IsTrue();
            var withOpen = new[]
            {
                new OddsSnapshot
                {
                    GameId = 99, Book = Book.DraftKings, Market = "TOTAL", Side = "over",
                    Line = 47.5, AmericanOdds = -110, PriceKind = PriceKind.Open,
                    ClvQuality = ClvQuality.OpenClose, AssumedJuice = true,
                    CapturedAt = tipoff.AddDays(-7),
                },
            };
            await Assert.That(EspnLineBackfill.ReplayShouldSkip(withOpen)).IsFalse();
        }
        finally
        {
            try { File.Delete(path); } catch { /* */ }
        }
    }
}
