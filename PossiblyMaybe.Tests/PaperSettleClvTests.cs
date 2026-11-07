using Microsoft.EntityFrameworkCore;
using PossiblyMaybe.Cli.Paper;
using PossiblyMaybe.Data;
using PossiblyMaybe.Domain;

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
        await Assert.That(row.CrossBookClose).IsFalse();
    }

    [Test]
    public async Task ApplySettle_CrossBookMeta_StoresClose_NullsClv()
    {
        var row = OpenRow(line: 2.5, side: "OVER");
        var meta = new ResolvedSettleClose(
            3.0, -110, 4.0, "Consensus", DateTimeOffset.UtcNow, 10, SameBook: false);
        PaperLedger.ApplySettle(row, "W", finalTotal: 4.0, closeLine: 3.0, closeAmerican: -110, meta);
        await Assert.That(row.CloseLine).IsEqualTo(3.0);
        await Assert.That(row.CloseBook).IsEqualTo("Consensus");
        await Assert.That(row.CrossBookClose).IsTrue();
        await Assert.That(row.ClvPts).IsNull();
        await Assert.That(SameBookClose.IsFresh(row.MinutesBeforeStart)).IsTrue();
    }

    [Test]
    public async Task ResolveSettle_PrefersSameBook_OverConsensus()
    {
        var whPath = Path.Combine(Path.GetTempPath(), $"pm-wh-{Guid.NewGuid():N}.db");
        try
        {
            await AppDbContext.EnsureCreatedAsync($"Data Source={whPath}");
            var whOpts = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={whPath}").Options;
            await using (var wh = new AppDbContext(whOpts))
            {
                var tip = DateTimeOffset.Parse("2026-10-28T23:30:00Z");
                var g = new Game
                {
                    Sport = "MLS",
                    Season = "2026",
                    SourceKey = "mls:2026-10-28:MIA:CIN",
                    TipoffUtc = tip,
                    HomeTeam = "MIA",
                    AwayTeam = "CIN",
                    HomeScore = 2,
                    AwayScore = 2,
                };
                wh.Games.Add(g);
                await wh.SaveChangesAsync();
                wh.OddsSnapshots.AddRange(
                    new OddsSnapshot
                    {
                        GameId = g.Id, Book = Book.Consensus, Market = "TOTAL", Side = "over",
                        Line = 2.5, AmericanOdds = -110, PriceKind = PriceKind.Close,
                        CapturedAt = tip.AddMinutes(-5),
                    },
                    new OddsSnapshot
                    {
                        GameId = g.Id, Book = Book.DraftKings, Market = "TOTAL", Side = "over",
                        Line = 3.25, AmericanOdds = -105, PriceKind = PriceKind.Close,
                        CapturedAt = tip.AddMinutes(-8),
                    },
                    new OddsSnapshot
                    {
                        GameId = g.Id, Book = Book.Kalshi, Market = "TOTAL", Side = "over",
                        Line = 3.0, AmericanOdds = 100, PriceKind = PriceKind.Close,
                        CapturedAt = tip.AddMinutes(-3),
                    });
                await wh.SaveChangesAsync();
            }

            var dk = await PaperLedger.ResolveSettleLinesAsync(
                whPath, null, "20261028 CIN@MIA", "MLS", PaperVenues.DraftKings);
            await Assert.That(dk.SameBook).IsTrue();
            await Assert.That(dk.CloseLine).IsEqualTo(3.25);
            await Assert.That(dk.CloseBook).IsEqualTo("DraftKings");
            await Assert.That(dk.MinutesBeforeStart).IsEqualTo(8.0);

            var kal = await PaperLedger.ResolveSettleLinesAsync(
                whPath, null, "20261028 CIN@MIA", "MLS", PaperVenues.Kalshi);
            await Assert.That(kal.SameBook).IsTrue();
            await Assert.That(kal.CloseLine).IsEqualTo(3.0);

            var rh = await PaperLedger.ResolveSettleLinesAsync(
                whPath, null, "20261028 CIN@MIA", "MLS", PaperVenues.Robinhood);
            await Assert.That(rh.SameBook).IsTrue();
            await Assert.That(rh.CloseBook).IsEqualTo("Kalshi");
            // RH fee widens Kalshi American
            await Assert.That(rh.CloseAmerican).IsNotEqualTo(100);

            // Crypto ticket with only DK+Kalshi+Consensus → cross-book
            var cdc = await PaperLedger.ResolveSettleLinesAsync(
                whPath, null, "20261028 CIN@MIA", "MLS", PaperVenues.CryptoCom);
            await Assert.That(cdc.SameBook).IsFalse();
            await Assert.That(cdc.CloseLine).IsNotNull();
            await Assert.That(cdc.CloseBook).IsNotEqualTo("CryptoCom");
        }
        finally
        {
            try { File.Delete(whPath); } catch { /* ignore */ }
        }
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
