using Microsoft.EntityFrameworkCore;
using PossiblyMaybe.Data;
using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Cli.Paper;

public static class PaperLedger
{
    public static string DefaultDbPath(string repoRoot) => Path.Combine(repoRoot, "paper.db");

    public static async Task<(AppDbContext Db, string Path)> OpenAsync(string dbPath, CancellationToken ct = default)
    {
        var cs = $"Data Source={dbPath}";
        await AppDbContext.EnsureCreatedAsync(cs, ct);
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(cs).Options;
        return (new AppDbContext(options), dbPath);
    }

    public static async Task<LedgerEntry> PlaceAsync(
        AppDbContext db,
        string gameKey,
        string side,
        double line,
        int americanOdds,
        double stakeDollars,
        double modelProb,
        CancellationToken ct = default)
    {
        side = side.ToUpperInvariant();
        if (side is not ("HOME" or "AWAY" or "OVER" or "UNDER"))
            throw new ArgumentException("side must be HOME, AWAY, OVER, or UNDER");
        if (stakeDollars <= 0)
            throw new ArgumentException("stake must be > 0");

        var row = new LedgerEntry
        {
            PlacedAt = DateTimeOffset.UtcNow,
            GameKey = gameKey,
            Market = ChampConfig.Market,
            Side = side,
            Line = line,
            AmericanOdds = americanOdds,
            StakeDollars = stakeDollars,
            ModelProb = modelProb,
            IsPaper = true,
        };
        db.LedgerEntries.Add(row);
        await db.SaveChangesAsync(ct);
        return row;
    }

    public static async Task<LedgerEntry> SettleAsync(
        AppDbContext db,
        int id,
        string result,
        double? closeLine,
        int? closeAmerican,
        CancellationToken ct = default)
    {
        result = result.ToUpperInvariant();
        if (result is not ("W" or "L" or "P"))
            throw new ArgumentException("result must be W, L, or P");

        var row = await db.LedgerEntries.FindAsync([id], ct)
            ?? throw new InvalidOperationException($"ledger id {id} not found");

        row.Result = result;
        row.CloseLine = closeLine;
        row.CloseAmerican = closeAmerican;
        row.PnL = result switch
        {
            "W" => row.StakeDollars * OddsMath.AmericanToDecimal(row.AmericanOdds) - row.StakeDollars,
            "L" => -row.StakeDollars,
            _ => 0,
        };
        if (closeLine is { } cl)
        {
            // OVER: higher close = better; UNDER/ATS home-style: lower line magnitude rules differ
            row.ClvPts = row.Side is "OVER" ? cl - row.Line
                : row.Side is "UNDER" ? row.Line - cl
                : row.Line - cl; // HOME/AWAY spread: betLine − closeLine
        }

        await db.SaveChangesAsync(ct);
        return row;
    }

    public static async Task PrintStatusAsync(AppDbContext db, CancellationToken ct = default)
    {
        var rows = await db.LedgerEntries.AsNoTracking().OrderBy(x => x.Id).ToListAsync(ct);
        var settled = rows.Where(r => r.Result is not null).ToList();
        var open = rows.Count - settled.Count;
        var pnl = settled.Sum(r => r.PnL ?? 0);
        var clv = settled.Where(r => r.ClvPts.HasValue).Select(r => r.ClvPts!.Value).ToList();
        var wins = settled.Count(r => r.Result == "W");
        var bank = ChampConfig.BankrollSeed + pnl;

        Console.WriteLine($"Paper bankroll: ${bank:F2} (seed {ChampConfig.BankrollSeed:F0} + PnL {pnl:+0.00;-0.00})");
        Console.WriteLine($"Tickets: {rows.Count}  open={open}  settled={settled.Count}  W={wins} L={settled.Count(r => r.Result == "L")} P={settled.Count(r => r.Result == "P")}");
        if (clv.Count > 0)
            Console.WriteLine($"Mean spread CLV: {clv.Average():+0.00;-0.00} pts (n={clv.Count})");
        Console.WriteLine($"Gate 2b: need ~1 month live paper; champ={ChampConfig.Market}/{ChampConfig.Model} edge≥{ChampConfig.MinAbsTotalEdge} EV≥{ChampConfig.MinEv:P0}");
        foreach (var r in rows.TakeLast(15))
        {
            var res = r.Result ?? "OPEN";
            var clvS = r.ClvPts is { } c ? $" clv={c:+0.0;-0.0}" : "";
            var pnlS = r.PnL is { } p ? $" pnl={p:+0.00;-0.00}" : "";
            Console.WriteLine($"  #{r.Id} {r.PlacedAt:yyyy-MM-dd} {r.GameKey} {r.Side} {r.Line:+0.0;-0.0} @{r.AmericanOdds} ${r.StakeDollars:F2} → {res}{pnlS}{clvS}");
        }
    }
}
