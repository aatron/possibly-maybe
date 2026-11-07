using Microsoft.EntityFrameworkCore;
using PossiblyMaybe.Data;
using PossiblyMaybe.Domain;
using PossiblyMaybe.Domain.SportPacks;

namespace PossiblyMaybe.Cli.Backtest;

/// <summary>
/// Tier A for <see cref="NflWind"/> alone: EPA+roof baseline vs same + wind ≥15 under bump.
/// Filled open→close only; holdout season burned (not used for selection).
/// </summary>
public static class NflWindTierA
{
    public const int NTrials = 1;

    public static async Task RunAsync(string dbPath, CancellationToken ct = default)
    {
        var pack = NflSportPack.Instance;
        var w = pack.MarketWeight;
        var minEv = pack.MinEv;
        var edge = pack.MinAbsTotalEdge;
        var holdout = pack.TierCHoldoutSeason;

        Console.WriteLine($"######## Tier A: {NflWind.ArmId} alone (EPA+roof ± wind≥{NflWind.WindMphThreshold}) ########");
        Console.WriteLine($"Gates (HOLD FAIL research): w={w} |exp−line|≥{edge} EV≥{minEv:P0}  AssumedJuice −110");
        Console.WriteLine($"requireOpenClose=true. Holdout {holdout} burned (not for selection). N_trials={NTrials}");

        var windCtx = await CountWindContextAsync(dbPath, ct);
        Console.WriteLine($"Weather DB: NflGameContexts={windCtx.Total} withWind={windCtx.WithWind} extreme(≥{NflWind.WindMphThreshold})={windCtx.Extreme}");
        if (windCtx.Total == 0)
            Console.WriteLine("shortcut: no NflGameContexts — wind arm is a no-op; pack+proof still ship.");

        var folds = await FoldsWithOpenCloseAsync(dbPath, pack.TierAFolds, holdout, ct);
        if (folds.Count == 0)
        {
            Console.WriteLine("NO FOLDS with filled O→C — Tier A not runnable on this DB.");
            Console.WriteLine($"VERDICT NFL {NflWind.ArmId} TierA=FAIL (no O→C folds)");
            return;
        }

        Console.WriteLine($"Folds: {string.Join(",", folds)}\n");
        Console.WriteLine(
            $"{"fold",6} {"n0",5} {"clv0",7} {"pnl0",7} |" +
            $" {"nW",5} {"clvW",7} {"pnlW",7} {"dPnL",7} {"dCLV",7}");

        var foldWins = 0;
        var nTot0 = 0;
        var nTotW = 0;
        double sumClv0 = 0, sumClvW = 0, sumPnl0 = 0, sumPnlW = 0;

        foreach (var season in folds)
        {
            var baseR = await NflTotalsBacktest.RunSeasonFoldAsync(
                dbPath, season, w, minEv, edge, applyWind: false, ct: ct);
            var windR = await NflTotalsBacktest.RunSeasonFoldAsync(
                dbPath, season, w, minEv, edge, applyWind: true, ct: ct);

            var dPnl = windR.PaperPnlUnits - baseR.PaperPnlUnits;
            var dClv = windR.MeanClv - baseR.MeanClv;
            if (dPnl > 0) foldWins++;

            nTot0 += baseR.Bets;
            nTotW += windR.Bets;
            sumClv0 += baseR.MeanClv * baseR.Bets;
            sumClvW += windR.MeanClv * windR.Bets;
            sumPnl0 += baseR.PaperPnlUnits;
            sumPnlW += windR.PaperPnlUnits;

            Console.WriteLine(
                $"{season,6} {baseR.Bets,5} {baseR.MeanClv,7:F2} {baseR.PaperPnlUnits,7:F1} |" +
                $" {windR.Bets,5} {windR.MeanClv,7:F2} {windR.PaperPnlUnits,7:F1} {dPnl,7:F1} {dClv,7:F2}");
        }

        var meanClv0 = nTot0 == 0 ? 0 : sumClv0 / nTot0;
        var meanClvW = nTotW == 0 ? 0 : sumClvW / nTotW;
        var majority = foldWins * 2 > folds.Count;
        var clvOk = meanClvW >= meanClv0 - 1e-12;
        var nOk = nTotW >= 40;
        var pass = majority && clvOk && nOk;

        Console.WriteLine();
        Console.WriteLine($"SUMMARY folds={folds.Count} winFolds={foldWins}/{folds.Count} n0={nTot0} nW={nTotW} pnl0={sumPnl0:F1} pnlW={sumPnlW:F1} meanClv0={meanClv0:F3} meanClvW={meanClvW:F3}");
        Console.WriteLine(
            $"VERDICT NFL {NflWind.ArmId} TierA={(pass ? "PASS" : "FAIL")} (majorityPnLΔ={majority}, clv≥base={clvOk}, n≥40={nOk})");
        Console.WriteLine("Live shadow: register arm after DailySlate emits NFL candidates; 1H/team via NflShadowMarkets.");
        Console.WriteLine("Do not place bets from this agent.");
    }

    static async Task<List<string>> FoldsWithOpenCloseAsync(
        string dbPath, IReadOnlyList<string> declared, string holdout, CancellationToken ct)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={dbPath}").Options;
        await using var db = new AppDbContext(options);
        var outFolds = new List<string>();
        foreach (var s in declared.Where(s => string.CompareOrdinal(s, holdout) < 0).OrderBy(s => s))
        {
            var gameIds = await db.Games.AsNoTracking()
                .Where(g => g.Sport == "NFL" && g.Season == s && g.HomeScore != null)
                .Select(g => g.Id)
                .ToListAsync(ct);
            if (gameIds.Count == 0) continue;
            var hasOpen = await db.OddsSnapshots.AsNoTracking()
                .AnyAsync(o => gameIds.Contains(o.GameId) && o.Market == "TOTAL"
                               && o.PriceKind == PriceKind.Open && o.ClvQuality == ClvQuality.OpenClose, ct);
            if (hasOpen) outFolds.Add(s);
        }
        return outFolds;
    }

    static async Task<(int Total, int WithWind, int Extreme)> CountWindContextAsync(string dbPath, CancellationToken ct)
    {
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={dbPath}").Options;
            await using var db = new AppDbContext(options);
            var rows = await db.NflGameContexts.AsNoTracking().ToListAsync(ct);
            var with = rows.Count(r => r.WindMph is not null);
            var extreme = rows.Count(r => NflWind.IsExtreme(r.WindMph));
            return (rows.Count, with, extreme);
        }
        catch
        {
            return (0, 0, 0);
        }
    }
}
