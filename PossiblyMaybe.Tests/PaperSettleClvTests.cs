using Microsoft.EntityFrameworkCore;
using PossiblyMaybe.Cli.Paper;
using PossiblyMaybe.Data;

namespace PossiblyMaybe.Tests;

public class PaperSettleClvTests
{
    [Test]
    public async Task ApplySettle_ScoreOnly_LeavesCloseNull_StoresFinal()
    {
        var row = OpenRow(line: 2.5, side: "OVER");
        PaperLedger.ApplySettle(row, "W", finalTotal: 4.0, closeLine: null, closeAmerican: null);
        await Assert.That(row.FinalTotal).IsEqualTo(4.0);
        await Assert.That(row.CloseLine).IsNull();
        await Assert.That(row.ClvPts).IsNull();
        await Assert.That(row.Result).IsEqualTo("W");
    }

    [Test]
    public async Task ApplySettle_MarketClose_SetsClv_NotFromFinal()
    {
        var row = OpenRow(line: 2.5, side: "OVER");
        PaperLedger.ApplySettle(row, "W", finalTotal: 4.0, closeLine: 3.0, closeAmerican: -110);
        await Assert.That(row.FinalTotal).IsEqualTo(4.0);
        await Assert.That(row.CloseLine).IsEqualTo(3.0);
        await Assert.That(row.ClvPts).IsEqualTo(0.5); // 3.0 − 2.5
    }

    [Test]
    public async Task Backfill_NullsFakeClose_RefillsWhenWarehouseCloseKnown()
    {
        var paperPath = Path.Combine(Path.GetTempPath(), $"pm-paper-{Guid.NewGuid():N}.db");
        var whPath = Path.Combine(Path.GetTempPath(), $"pm-wh-{Guid.NewGuid():N}.db");
        try
        {
            await AppDbContext.EnsureCreatedAsync($"Data Source={whPath}");
            var whOpts = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={whPath}").Options;
            await using (var wh = new AppDbContext(whOpts))
            {
                var g = new Game
                {
                    Sport = "MLS",
                    Season = "2026",
                    SourceKey = "mls:2026-10-28:MIA:CIN",
                    TipoffUtc = DateTimeOffset.Parse("2026-10-28T23:30:00Z"),
                    HomeTeam = "MIA",
                    AwayTeam = "CIN",
                    HomeScore = 2,
                    AwayScore = 2,
                };
                wh.Games.Add(g);
                await wh.SaveChangesAsync();
                wh.OddsSnapshots.Add(new OddsSnapshot
                {
                    GameId = g.Id,
                    Book = Book.DraftKings,
                    Market = "TOTAL",
                    Side = "over",
                    Line = 3.25,
                    AmericanOdds = -110,
                    PriceKind = PriceKind.Close,
                    CapturedAt = g.TipoffUtc.AddMinutes(-30),
                });
                await wh.SaveChangesAsync();
            }

            var (paper, _) = await PaperLedger.OpenAsync(paperPath);
            await using (paper)
            {
                paper.LedgerEntries.Add(new LedgerEntry
                {
                    PlacedAt = DateTimeOffset.UtcNow,
                    GameKey = "20261028 CIN@MIA",
                    Market = "TOTAL",
                    Side = "OVER",
                    Line = 2.5,
                    AmericanOdds = -110,
                    StakeDollars = 10,
                    ModelProb = 0.55,
                    CloseLine = 4.0, // fake: final masquerading as close
                    ClvPts = 1.5,
                    Result = "W",
                    PnL = 9.09,
                    Scheme = "Flat",
                    TicketGroup = "t1",
                    Sport = "MLS",
                    Venue = "DK",
                });
                paper.LedgerEntries.Add(new LedgerEntry
                {
                    PlacedAt = DateTimeOffset.UtcNow,
                    GameKey = "20261028 NOCLOSE@GAME",
                    Market = "TOTAL",
                    Side = "UNDER",
                    Line = 3.5,
                    AmericanOdds = -110,
                    StakeDollars = 10,
                    ModelProb = 0.55,
                    CloseLine = 2.0, // fake final, no warehouse close
                    ClvPts = 1.5,
                    Result = "W",
                    PnL = 9.09,
                    Scheme = "Flat",
                    TicketGroup = "t2",
                    Sport = "MLS",
                    Venue = "DK",
                });
                await paper.SaveChangesAsync();

                var (n, with) = await PaperLedger.BackfillLiveClvAsync(paper, whPath, snapshotsDbPath: null);
                await Assert.That(n).IsEqualTo(2);
                await Assert.That(with).IsEqualTo(1);

                var known = paper.LedgerEntries.Single(r => r.GameKey.Contains("CIN@MIA"));
                await Assert.That(known.CloseLine).IsEqualTo(3.25);
                await Assert.That(known.FinalTotal).IsEqualTo(4.0);
                await Assert.That(known.ClvPts).IsEqualTo(0.75);

                var unknown = paper.LedgerEntries.Single(r => r.GameKey.Contains("NOCLOSE"));
                await Assert.That(unknown.CloseLine).IsNull();
                await Assert.That(unknown.ClvPts).IsNull();
                await Assert.That(unknown.FinalTotal).IsEqualTo(2.0); // preserved prior fake close as final
            }
        }
        finally
        {
            try { File.Delete(paperPath); } catch { /* ignore */ }
            try { File.Delete(whPath); } catch { /* ignore */ }
        }
    }

    static LedgerEntry OpenRow(double line, string side) => new()
    {
        PlacedAt = DateTimeOffset.UtcNow,
        GameKey = "20261107 AAA@BBB",
        Market = "TOTAL",
        Side = side,
        Line = line,
        AmericanOdds = -110,
        StakeDollars = 10,
        ModelProb = 0.55,
        Scheme = "Flat",
        TicketGroup = "g",
        Sport = "MLS",
        Venue = "DK",
    };
}
