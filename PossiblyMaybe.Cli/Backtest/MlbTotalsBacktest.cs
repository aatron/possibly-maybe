using Microsoft.EntityFrameworkCore;
using PossiblyMaybe.Data;
using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Cli.Backtest;

/// <summary>
/// MLB totals: prior-season SP FIP × home park factor → expected runs, blended with open total.
/// Holdout = tipoff on/after 2025-09-01 (locked before metric peek in pack notes).
/// </summary>
public static class MlbTotalsBacktest
{
    public static readonly DateTimeOffset HoldoutStart =
        new(2025, 9, 1, 0, 0, 0, TimeSpan.Zero);

    public static async Task<(Gate1aResult Train, Gate1aResult Hold)> RunAsync(
        string dbPath, double marketWeight, double minEv, double minAbsEdge,
        string? parkFactorsPath = null, CancellationToken ct = default)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={dbPath}").Options;
        await using var db = new AppDbContext(options);

        var games = (await db.Games.AsNoTracking()
                .Where(g => g.Sport == "MLB" && g.HomeScore != null)
                .ToListAsync(ct))
            .OrderBy(g => g.TipoffUtc).ThenBy(g => g.Id)
            .ToList();
        var odds = await db.OddsSnapshots.AsNoTracking()
            .Where(o => o.Market == "TOTAL")
            .ToListAsync(ct);
        var byOdds = odds.GroupBy(o => o.GameId).ToDictionary(g => g.Key, g => g.ToList());
        Dictionary<int, GamePitching> pitch = [];
        try { pitch = await db.GamePitchings.AsNoTracking().ToDictionaryAsync(p => p.GameId, ct); }
        catch { /* */ }
        var park = MlbParkFactors.Load(parkFactorsPath);

        const double lgRpg = 4.5;
        const double lgFip = 4.0;
        var scoring = new ScoringBook(alpha: 0.12, leagueInit: 9.0); // team runs EMA fallback
        var train = new Acc("mlb_train");
        var hold = new Acc("mlb_holdout_sep");

        foreach (var g in games)
        {
            byOdds.TryGetValue(g.Id, out var snaps);
            snaps ??= [];
            var open = snaps.FirstOrDefault(s => s.PriceKind == PriceKind.Open);
            var close = snaps.FirstOrDefault(s => s.PriceKind == PriceKind.Close);
            if (open?.Line is not { } line || close?.Line is not { } closeLine) continue;
            if (Math.Abs(line - closeLine) < 0.25) continue;

            double modelExp;
            if (pitch.TryGetValue(g.Id, out var sp))
            {
                // Each offense faces the other starter's FIP
                modelExp = lgRpg * (sp.AwayFipPrior / lgFip) + lgRpg * (sp.HomeFipPrior / lgFip);
            }
            else
                modelExp = scoring.ExpectedTotal(g.HomeTeam, g.AwayTeam);

            modelExp = MlbParkFactors.Scale(modelExp, g.HomeTeam, park);
            var exp = marketWeight * line + (1 - marketWeight) * modelExp;
            var edge = exp - line;
            var acc = g.TipoffUtc >= HoldoutStart ? hold : train;
            var juice = -110;
            var ou = TotalsMath.OuResult(g.HomeScore!.Value, g.AwayScore!.Value, line);
            var clv = TotalsMath.OverClvPoints(line, closeLine);

            if (Math.Abs(edge) >= minAbsEdge)
            {
                if (edge > 0)
                {
                    var p = TotalsMath.OverProb(exp, line, sharpness: 0.45);
                    var ev = OddsMath.ExpectedValue(p, juice);
                    if (ev >= minEv)
                        acc.AddBet(clv, ou > 0, TotalsMath.OuPnL(juice, ou));
                }
                else
                {
                    var p = 1 - TotalsMath.OverProb(exp, line, sharpness: 0.45);
                    var ev = OddsMath.ExpectedValue(p, juice);
                    if (ev >= minEv)
                        acc.AddBet(-clv, ou < 0, TotalsMath.OuPnL(juice, -ou));
                }
            }

            acc.AddGame();
            scoring.Update(g.HomeTeam, g.AwayTeam, g.HomeScore!.Value, g.AwayScore!.Value);
        }

        return (train.ToResult(), hold.ToResult());
    }

    public static async Task SweepAsync(string dbPath, string? parkFactorsPath = null, CancellationToken ct = default)
    {
        Console.WriteLine("######## MLB TOTALS (SP FIP × home park) ########");
        Console.WriteLine($"Holdout start: {HoldoutStart:yyyy-MM-dd}");
        var parkPath = parkFactorsPath ?? "";
        var nPf = MlbParkFactors.Load(string.IsNullOrEmpty(parkPath) ? null : parkPath).Count;
        Console.WriteLine($"Park factors: {(nPf > 0 ? $"{nPf} teams from {parkPath}" : "none (PF=100)")}");
        var rows = new List<(double W, double Edge, Gate1aResult Train, Gate1aResult Hold)>();
        foreach (var w in new[] { 0.4, 0.5, 0.6, 0.7 })
        foreach (var edge in new[] { 0.5, 0.75, 1.0, 1.25, 1.5 })
        {
            var (tr, ho) = await RunAsync(dbPath, w, 0.04, edge, parkFactorsPath, ct);
            rows.Add((w, edge, tr, ho));
            Console.WriteLine(
                $"w={w:F2} edge={edge:F2}  train n={tr.Bets} clv={tr.MeanClv:F2} pnl={tr.PaperPnlUnits:F1} | hold n={ho.Bets} clv={ho.MeanClv:F2} pnl={ho.PaperPnlUnits:F1}");
        }

        var eligible = rows
            .Where(r => r.Train.MeanClv > 0 && r.Train.PaperPnlUnits > 0 && r.Train.Bets is >= 80 and <= 1200)
            .OrderByDescending(r => r.Train.PaperPnlUnits)
            .ToList();
        if (eligible.Count == 0)
        {
            Console.WriteLine("KILL: HOLD FAIL — no MLB train config in band.");
            return;
        }

        var best = eligible[0];
        Console.WriteLine($"=== MLB CHAMP w={best.W:F2} edge={best.Edge:F2} ===");
        Console.WriteLine($"TRAIN  n={best.Train.Bets} clv={best.Train.MeanClv:F3} hit={best.Train.HitRate:P1} pnl={best.Train.PaperPnlUnits:F1}");
        Console.WriteLine($"HOLDOUT n={best.Hold.Bets} clv={best.Hold.MeanClv:F3} hit={best.Hold.HitRate:P1} pnl={best.Hold.PaperPnlUnits:F1}");
        var pass = best.Hold.PaperPnlUnits > 0 && best.Hold.MeanClv > 0 && best.Hold.Bets >= 40;
        Console.WriteLine(pass
            ? "KILL: HOLD PASS — MLB SP-FIP totals (paper)."
            : "KILL: HOLD FAIL — MLB not money yet.");
    }

    private sealed class Acc(string slice)
    {
        private int _games, _bets, _hits;
        private double _clv, _pnl;
        public void AddGame() => _games++;
        public void AddBet(double clv, bool won, double pnl)
        {
            _bets++; _clv += clv; _pnl += pnl; if (won) _hits++;
        }
        public Gate1aResult ToResult() => new()
        {
            Slice = slice, GamesScored = _games, Bets = _bets,
            MeanClv = _bets == 0 ? 0 : _clv / _bets, ClvUnit = "runs",
            HitRate = _bets == 0 ? 0 : (double)_hits / _bets, PaperPnlUnits = _pnl,
        };
    }
}
