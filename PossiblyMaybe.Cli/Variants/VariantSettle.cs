using Microsoft.EntityFrameworkCore;
using PossiblyMaybe.Data;
using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Cli.Variants;

/// <summary>
/// Auto-settle shadow picks from warehouse finals. No user confirmation (hypothetical ledger).
/// Closing line from OddsSnapshots Close when present; else null CLV.
/// </summary>
public static class VariantSettle
{
    public static async Task<int> SettleOpenAsync(
        string variantsDbPath,
        string warehouseDbPath,
        CancellationToken ct = default)
    {
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

                double? closeLine = null;
                var snaps = await wdb.OddsSnapshots.AsNoTracking()
                    .Where(o => o.GameId == game.Id && o.Market == "TOTAL")
                    .ToListAsync(ct);
                closeLine = snaps.FirstOrDefault(s => s.PriceKind == PriceKind.Close)?.Line
                    ?? snaps.FirstOrDefault(s => s.PriceKind == PriceKind.Open)?.Line;

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

                    row.Result = result;
                    row.PnL = pnl;
                    row.CloseLine = closeLine;
                    if (closeLine is { } cl)
                    {
                        var overClv = TotalsMath.OverClvPoints(row.Line.Value, cl);
                        row.ClvPts = sideOver ? overClv : -overClv;
                    }
                    row.SettledAt = DateTimeOffset.UtcNow;
                    settled++;
                }
            }

            if (settled > 0)
                await vdb.SaveChangesAsync(ct);
            return settled;
        }
    }

    private static async Task<Game?> FindGameAsync(
        AppDbContext db, string sport, string yyyymmdd, string home, string away, CancellationToken ct)
    {
        if (!DateTime.TryParseExact(yyyymmdd, "yyyyMMdd", null,
                System.Globalization.DateTimeStyles.None, out var day))
            return null;
        var start = new DateTimeOffset(DateTime.SpecifyKind(day, DateTimeKind.Utc));
        var end = start.AddDays(1);
        var games = await db.Games.AsNoTracking()
            .Where(g => g.Sport == sport && g.TipoffUtc >= start && g.TipoffUtc < end)
            .ToListAsync(ct);
        return games.FirstOrDefault(g =>
            g.HomeTeam.Equals(home, StringComparison.OrdinalIgnoreCase)
            && g.AwayTeam.Equals(away, StringComparison.OrdinalIgnoreCase));
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
