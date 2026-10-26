using Microsoft.EntityFrameworkCore;
using PossiblyMaybe.Data;
using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Cli.Backtest;

/// <summary>
/// NFL totals: team scoring EMA + roof/wind adjustments.
/// CLV only on OpenClose rows; close-only games scored for PnL (model vs close) with CLV=0.
/// Holdout season = 2024 (complete) for volume; 2026 open/close is too thin alone.
/// </summary>
public static class NflTotalsBacktest
{
    public const string HoldoutSeason = "2024";

    public static async Task<(Gate1aResult Train, Gate1aResult Hold)> RunAsync(
        string dbPath, double marketWeight, double minEv, double minAbsEdge, CancellationToken ct = default)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={dbPath}").Options;
        await using var db = new AppDbContext(options);

        var games = (await db.Games.AsNoTracking()
                .Where(g => g.Sport == "NFL" && g.HomeScore != null)
                .ToListAsync(ct))
            .OrderBy(g => g.TipoffUtc).ThenBy(g => g.Id)
            .ToList();
        var odds = await db.OddsSnapshots.AsNoTracking().Where(o => o.Market == "TOTAL").ToListAsync(ct);
        var byOdds = odds.GroupBy(o => o.GameId).ToDictionary(g => g.Key, g => g.ToList());
        Dictionary<int, NflGameContext> ctx = [];
        try { ctx = await db.NflGameContexts.AsNoTracking().ToDictionaryAsync(x => x.GameId, ct); }
        catch { /* */ }

        var scoring = new ScoringBook(alpha: 0.15, leagueInit: 44);
        var train = new Acc("nfl_train");
        var hold = new Acc("nfl_holdout_2024");

        foreach (var g in games)
        {
            byOdds.TryGetValue(g.Id, out var snaps);
            snaps ??= [];
            var open = snaps.FirstOrDefault(s => s.PriceKind == PriceKind.Open);
            var close = snaps.FirstOrDefault(s => s.PriceKind == PriceKind.Close)
                        ?? snaps.OrderByDescending(s => s.CapturedAt).FirstOrDefault();
            if (close?.Line is not { } closeLine) continue;

            // Bet price: open when present, else close (CloseOnly — no true CLV)
            var betLine = open?.Line ?? closeLine;
            var hasClv = open?.Line is not null && open.ClvQuality == ClvQuality.OpenClose
                         && Math.Abs(betLine - closeLine) >= 0.25;

            var modelExp = scoring.ExpectedTotal(g.HomeTeam, g.AwayTeam);
            if (ctx.TryGetValue(g.Id, out var c))
            {
                var roof = (c.Roof ?? "").ToLowerInvariant();
                if (roof is "dome" or "closed" or "retractable")
                    modelExp += 1.5; // industry dome bump
                if (c.WindMph is >= 15)
                    modelExp -= 3.0; // wind under lever
            }

            var market = betLine;
            var exp = marketWeight * market + (1 - marketWeight) * modelExp;
            var edge = exp - market;
            var acc = g.Season == HoldoutSeason ? hold : train;
            var juice = -110;
            var ou = TotalsMath.OuResult(g.HomeScore!.Value, g.AwayScore!.Value, betLine);
            var clv = hasClv ? TotalsMath.OverClvPoints(betLine, closeLine) : 0;

            if (Math.Abs(edge) >= minAbsEdge)
            {
                if (edge > 0)
                {
                    var p = TotalsMath.OverProb(exp, market, sharpness: 0.2);
                    var ev = OddsMath.ExpectedValue(p, juice);
                    if (ev >= minEv)
                        acc.AddBet(clv, ou > 0, TotalsMath.OuPnL(juice, ou), hasClv);
                }
                else
                {
                    var p = 1 - TotalsMath.OverProb(exp, market, sharpness: 0.2);
                    var ev = OddsMath.ExpectedValue(p, juice);
                    if (ev >= minEv)
                        acc.AddBet(hasClv ? -clv : 0, ou < 0, TotalsMath.OuPnL(juice, -ou), hasClv);
                }
            }

            acc.AddGame();
            scoring.Update(g.HomeTeam, g.AwayTeam, g.HomeScore!.Value, g.AwayScore!.Value);
        }

        return (train.ToResult(), hold.ToResult());
    }

    public static async Task SweepAsync(string dbPath, CancellationToken ct = default)
    {
        Console.WriteLine("######## NFL TOTALS (scoring + roof/wind) ########");
        Console.WriteLine("NOTE: most history is CloseOnly — CLV sparse until more open/close lands.");
        var rows = new List<(double W, double Edge, Gate1aResult Train, Gate1aResult Hold)>();
        foreach (var w in new[] { 0.5, 0.6, 0.7, 0.85 })
        foreach (var edge in new[] { 2.0, 3.0, 4.0, 5.0, 6.0 })
        {
            var (tr, ho) = await RunAsync(dbPath, w, 0.04, edge, ct);
            rows.Add((w, edge, tr, ho));
            Console.WriteLine(
                $"w={w:F2} edge={edge:F1}  train n={tr.Bets} clv={tr.MeanClv:F2} pnl={tr.PaperPnlUnits:F1} | hold n={ho.Bets} clv={ho.MeanClv:F2} pnl={ho.PaperPnlUnits:F1}");
        }

        var eligible = rows
            .Where(r => r.Train.PaperPnlUnits > 0 && r.Train.Bets is >= 80 and <= 2000)
            .OrderByDescending(r => r.Train.PaperPnlUnits)
            .ToList();
        if (eligible.Count == 0)
        {
            Console.WriteLine("KILL: HOLD FAIL — no NFL train config.");
            return;
        }

        var best = eligible[0];
        Console.WriteLine($"=== NFL CHAMP (train max PnL) w={best.W:F2} edge={best.Edge:F1} ===");
        Console.WriteLine($"TRAIN  n={best.Train.Bets} clv={best.Train.MeanClv:F3} hit={best.Train.HitRate:P1} pnl={best.Train.PaperPnlUnits:F1}");
        Console.WriteLine($"HOLDOUT n={best.Hold.Bets} clv={best.Hold.MeanClv:F3} hit={best.Hold.HitRate:P1} pnl={best.Hold.PaperPnlUnits:F1}");
        // Require PnL; CLV optional until open/close denser
        var pass = best.Hold.PaperPnlUnits > 0 && best.Hold.Bets >= 30;
        Console.WriteLine(pass
            ? "KILL: HOLD PASS (PnL) — NFL provisional; CLV weak until open/close history grows."
            : "KILL: HOLD FAIL — NFL not money yet.");
    }

    private sealed class Acc(string slice)
    {
        private int _games, _bets, _hits, _clvN;
        private double _clv, _pnl;
        public void AddGame() => _games++;
        public void AddBet(double clv, bool won, double pnl, bool hasClv)
        {
            _bets++; _pnl += pnl; if (won) _hits++;
            if (hasClv) { _clv += clv; _clvN++; }
        }
        public Gate1aResult ToResult() => new()
        {
            Slice = slice, GamesScored = _games, Bets = _bets,
            MeanClv = _clvN == 0 ? 0 : _clv / _clvN, ClvUnit = "pts",
            HitRate = _bets == 0 ? 0 : (double)_hits / _bets, PaperPnlUnits = _pnl,
        };
    }
}
