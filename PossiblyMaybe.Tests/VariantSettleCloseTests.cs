using Microsoft.EntityFrameworkCore;
using PossiblyMaybe.Cli.Variants;
using PossiblyMaybe.Data;
using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Tests;

/// <summary>D1: shadow settle stores same-book true close from tracker when warehouse is Consensus-only.</summary>
public class VariantSettleCloseTests
{
    [Test]
    public async Task Settle_UsesTrackerEspnClose_WhenWarehouseOnlyHasConsensus()
    {
        var whPath = Path.Combine(Path.GetTempPath(), $"pm-wh-{Guid.NewGuid():N}.db");
        var vPath = Path.Combine(Path.GetTempPath(), $"pm-var-{Guid.NewGuid():N}.db");
        var snapPath = Path.Combine(Path.GetTempPath(), $"pm-snap-{Guid.NewGuid():N}.db");
        try
        {
            await AppDbContext.EnsureCreatedAsync($"Data Source={whPath}");
            var tip = DateTimeOffset.Parse("2026-11-08T18:15:00Z");
            var whOpts = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={whPath}").Options;
            await using (var wh = new AppDbContext(whOpts))
            {
                var g = new Game
                {
                    Sport = "LIGUE1",
                    Season = "2026-2027",
                    SourceKey = "ligue1:2026-11-08:Angers:Nice",
                    TipoffUtc = tip,
                    HomeTeam = "Angers",
                    AwayTeam = "Nice",
                    HomeScore = 1,
                    AwayScore = 2,
                };
                wh.Games.Add(g);
                await wh.SaveChangesAsync();
                wh.OddsSnapshots.Add(new OddsSnapshot
                {
                    GameId = g.Id, Book = Book.Consensus, Market = "TOTAL", Side = "over",
                    Line = 2.5, AmericanOdds = -110, PriceKind = PriceKind.Close,
                    CapturedAt = tip.AddMinutes(-30),
                });
                await wh.SaveChangesAsync();
            }

            await using (var con = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={snapPath}"))
            {
                await con.OpenAsync();
                await using var cmd = con.CreateCommand();
                cmd.CommandText = """
                    CREATE TABLE line_snapshots(
                      captured_at TEXT NOT NULL, sport TEXT NOT NULL, game_key TEXT NOT NULL,
                      start_utc TEXT NOT NULL, minutes_to_start REAL, venue TEXT NOT NULL,
                      source TEXT, line REAL NOT NULL, over_bid REAL, over_ask REAL,
                      under_bid REAL, under_ask REAL, over_american INTEGER, under_american INTEGER,
                      open_line REAL, ticker TEXT);
                    INSERT INTO line_snapshots VALUES
                      ('2026-11-08T17:00:00+00:00','LIGUE1','20261108 NICE@ANG 1115',
                       '2026-11-08T18:15:00+00:00',75,'espn','DK',3.25,null,null,null,null,-110,-110,null,null);
                    """;
                await cmd.ExecuteNonQueryAsync();
            }

            var (vdb, _) = await VariantLedger.OpenAsync(vPath);
            await using (vdb)
            {
                vdb.ShadowEntries.Add(new ShadowEntry
                {
                    VariantId = "V0",
                    Sport = "LIGUE1",
                    GameKey = "20261108 Nice@Angers",
                    Side = "OVER",
                    Line = 2.5,
                    AmericanOdds = -110,
                    ModelProb = 0.55,
                    Ev = 0.05,
                    EvaluatedAt = tip.AddHours(-6),
                });
                await vdb.SaveChangesAsync();
            }

            var n = await VariantSettle.SettleOpenAsync(vPath, whPath, snapPath);
            await Assert.That(n).IsEqualTo(1);

            var (vdb2, _) = await VariantLedger.OpenAsync(vPath);
            await using (vdb2)
            {
                var row = await vdb2.ShadowEntries.SingleAsync();
                await Assert.That(row.Result).IsEqualTo("W");
                await Assert.That(row.CloseLine).IsEqualTo(3.25);
                await Assert.That(row.CloseBook).IsEqualTo("DraftKings");
                await Assert.That(row.CrossBookClose).IsFalse();
                await Assert.That(row.ClvPts).IsEqualTo(0.75); // 3.25 − 2.5
            }
        }
        finally
        {
            foreach (var p in new[] { whPath, vPath, snapPath })
                try { File.Delete(p); } catch { /* ignore */ }
        }
    }
}
