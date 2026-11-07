using Microsoft.EntityFrameworkCore;
using PossiblyMaybe.Cli.Import;
using PossiblyMaybe.Cli.Paper;
using PossiblyMaybe.Data;
using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Cli.Variants;

/// <summary>
/// Auto-settle shadow picks from warehouse finals. No user confirmation (hypothetical ledger).
/// Closing line prefers DraftKings same-book (R3); tracker line_snapshots fill gaps (D1);
/// cross-book closes flagged, CLV null.
/// </summary>
public static class VariantSettle
{
    public static async Task<int> SettleOpenAsync(
        string variantsDbPath,
        string warehouseDbPath,
        string? snapshotsDbPath = null,
        CancellationToken ct = default)
    {
        snapshotsDbPath ??= DefaultSnapshotsPath(warehouseDbPath);

        var (vdb, _) = await VariantLedger.OpenAsync(variantsDbPath, ct);
        await using (vdb)
        {
            var open = await vdb.ShadowEntries
                .Where(e => e.SkipReason == null && e.Result == null && e.Side != null && e.Line != null)
                .ToListAsync(ct);
            if (open.Count == 0) return 0;

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={warehouseDbPath}").Options;
            await using var wdb = new AppDbContext(options);

            var settled = 0;
            foreach (var group in open.GroupBy(e => e.GameKey, StringComparer.OrdinalIgnoreCase))
            {
                var key = group.Key;
                if (!TryParseGameKey(key, out var day, out var away, out var home))
                    continue;
                var sport = group.First().Sport;
                var game = await FindGameAsync(wdb, sport, day, home, away, ct);
                if (game?.HomeScore is not { } hs || game.AwayScore is not { } as_)
                    continue;

                var snaps = await wdb.OddsSnapshots.AsNoTracking()
                    .Where(o => o.GameId == game.Id && o.Market == "TOTAL" && o.Line != null)
                    .ToListAsync(ct);
                var same = PickClose(snaps, Book.DraftKings, game.TipoffUtc);
                double? closeLine;
                string? closeBook;
                DateTimeOffset? captured;
                double? mins;
                var sameBook = false;

                if (same is not null)
                {
                    sameBook = true;
                    closeLine = same.Line;
                    closeBook = SameBookClose.BookLabel(same.Book);
                    captured = same.CapturedAt;
                    mins = (game.TipoffUtc - same.CapturedAt).TotalMinutes;
                }
                else if (!string.IsNullOrEmpty(snapshotsDbPath)
                         && LineTrackerImporter.TryCloseFromSnapshots(
                             snapshotsDbPath, sport, key, SameBookClose.TrackerVenueForBook(Book.DraftKings))
                             is { } tr)
                {
                    // Tracker ESPN → DraftKings same-book (D1); works for abbr and full-name keys.
                    sameBook = true;
                    closeLine = tr.Line;
                    closeBook = SameBookClose.BookLabel(Book.DraftKings);
                    mins = tr.MinutesToStart;
                    captured = mins is { } m ? game.TipoffUtc.AddMinutes(-m) : null;
                }
                else
                {
                    var other = snaps
                        .Where(s => s.PriceKind == PriceKind.Close || s.CapturedAt <= game.TipoffUtc)
                        .OrderByDescending(s => s.PriceKind == PriceKind.Close)
                        .ThenByDescending(s => s.CapturedAt)
                        .FirstOrDefault();
                    closeLine = other?.Line;
                    closeBook = other is null ? null : SameBookClose.BookLabel(other.Book);
                    captured = other?.CapturedAt;
                    mins = other is null ? null : (game.TipoffUtc - other.CapturedAt).TotalMinutes;
                }

                foreach (var row in group)
                {
                    var ou = TotalsMath.OuResult(hs, as_, row.Line!.Value);
                    var sideOver = row.Side!.Equals("OVER", StringComparison.OrdinalIgnoreCase);
                    string result;
                    double pnl;
                    if (ou == 0)
                    {
                        result = "P";
                        pnl = 0;
                    }
                    else if ((sideOver && ou > 0) || (!sideOver && ou < 0))
                    {
                        result = "W";
                        pnl = TotalsMath.OuPnL(row.AmericanOdds, 1);
                    }
                    else
                    {
                        result = "L";
                        pnl = -1;
                    }

                    double? clvPts = null;
                    if (sameBook && closeLine is { } cl)
                    {
                        var overClv = TotalsMath.OverClvPoints(row.Line.Value, cl);
                        clvPts = sideOver ? overClv : -overClv;
                    }

                    row.Result = result;
                    row.PnL = pnl;
                    row.CloseLine = closeLine;
                    row.CloseBook = closeBook;
                    row.CloseCapturedAt = captured;
                    row.MinutesBeforeStart = mins;
                    row.CrossBookClose = closeLine is not null && !sameBook;
                    row.ClvPts = clvPts;
                    row.SettledAt = DateTimeOffset.UtcNow;
                    settled++;

                    // R2: settle newest unsettled DecisionLog row; never mutate the log.
                    var logId = await vdb.DecisionLogEntries.AsNoTracking()
                        .Where(e => e.VariantId == row.VariantId && e.GameKey == row.GameKey && e.SkipReason == null)
                        .OrderByDescending(e => e.Id)
                        .Select(e => (long?)e.Id)
                        .FirstOrDefaultAsync(ct);
                    if (logId is { } lid
                        && !await vdb.DecisionSettlements.AnyAsync(s => s.DecisionLogId == lid, ct))
                    {
                        vdb.DecisionSettlements.Add(new DecisionSettlement
                        {
                            DecisionLogId = lid,
                            Result = result,
                            PnL = pnl,
                            CloseLine = closeLine,
                            ClvPts = clvPts,
                            CloseBook = closeBook,
                            CloseCapturedAt = captured,
                            MinutesBeforeStart = mins,
                            CrossBookClose = closeLine is not null && !sameBook,
                            SettledAt = DateTimeOffset.UtcNow,
                        });
                    }
                }
            }

            if (settled > 0)
                await vdb.SaveChangesAsync(ct);
            return settled;
        }
    }

    static string? DefaultSnapshotsPath(string warehouseDbPath)
    {
        var root = Path.GetDirectoryName(Path.GetFullPath(warehouseDbPath)) ?? ".";
        var p = Path.Combine(root, "data", "line_snapshots.db");
        return File.Exists(p) ? p : null;
    }

    static OddsSnapshot? PickClose(
        IReadOnlyList<OddsSnapshot> snaps, Book want, DateTimeOffset tipoff)
    {
        var bookSnaps = snaps.Where(s => s.Book == want).ToList();
        if (bookSnaps.Count == 0) return null;
        var labelled = bookSnaps
            .Where(s => s.PriceKind == PriceKind.Close)
            .OrderByDescending(s => s.CapturedAt)
            .FirstOrDefault();
        if (labelled is not null) return labelled;
        return bookSnaps
            .Where(s => s.CapturedAt <= tipoff)
            .OrderByDescending(s => s.CapturedAt)
            .FirstOrDefault();
    }

    private static async Task<Game?> FindGameAsync(
        AppDbContext db, string sport, string yyyymmdd, string home, string away, CancellationToken ct)
    {
        if (!DateTime.TryParseExact(yyyymmdd, "yyyyMMdd", null,
                System.Globalization.DateTimeStyles.None, out var day))
            return null;
        // Team match in SQL; tipoff ±1 day in memory (SQLite DateTimeOffset ranges flake — same as PaperLedger).
        var games = await db.Games.AsNoTracking()
            .Where(g => g.Sport == sport && g.HomeTeam == home && g.AwayTeam == away)
            .ToListAsync(ct);
        var start = new DateTimeOffset(DateTime.SpecifyKind(day.AddDays(-1), DateTimeKind.Utc));
        var end = start.AddDays(3);
        var hits = games.Where(g => g.TipoffUtc >= start && g.TipoffUtc < end).ToList();
        if (hits.Count == 0) return null;
        var dayOnly = DateOnly.FromDateTime(day);
        return hits
            .OrderBy(g => Math.Abs(
                DateOnly.FromDateTime(g.TipoffUtc.UtcDateTime).DayNumber - dayOnly.DayNumber))
            .ThenBy(g => g.TipoffUtc)
            .First();
    }

    private static bool TryParseGameKey(string key, out string day, out string away, out string home)
    {
        day = ""; away = ""; home = "";
        var sp = key.IndexOf(' ');
        if (sp <= 0) return false;
        day = key[..sp];
        var rest = key[(sp + 1)..];
        var at = rest.IndexOf('@');
        if (at <= 0) return false;
        away = rest[..at];
        home = rest[(at + 1)..];
        return day.Length == 8;
    }
}
