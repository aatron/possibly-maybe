using Microsoft.EntityFrameworkCore;
using PossiblyMaybe.Data;
using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Cli.Backtest;

/// <summary>
/// Goals-EMA totals for any soccer Sport code (EFL Championship, Liga MX, …).
/// Holdout locked: train seasons &lt; holdout; skip seasons after holdout.
/// </summary>
public static class SoccerLeagueTotalsBacktest
{
    public static async Task<(Gate1aResult Train, Gate1aResult Hold)> RunAsync(
        string dbPath, string sport, string holdoutSeason, double marketWeight, double minEv, double minAbsEdge,
        bool usePlatt = false, CancellationToken ct = default)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={dbPath}").Options;
        await using var db = new AppDbContext(options);

        var games = (await db.Games.AsNoTracking()
                .Where(g => g.Sport == sport && g.HomeScore != null)
                .ToListAsync(ct))
            .OrderBy(g => g.TipoffUtc).ThenBy(g => g.Id)
            .ToList();
        var odds = await db.OddsSnapshots.AsNoTracking().Where(o => o.Market == "TOTAL").ToListAsync(ct);
        var byOdds = odds.GroupBy(o => o.GameId).ToDictionary(g => g.Key, g => g.ToList());

        var goalsBook = new ScoringBook(alpha: 0.18, leagueInit: 2.7);
        OuPlattCalibrator? platt = usePlatt ? new OuPlattCalibrator() : null;
        var train = new Acc($"{sport}_train");
        var hold = new Acc($"{sport}_holdout_{holdoutSeason}");

        foreach (var g in games)
        {
            var season = g.Season ?? "";
            if (string.CompareOrdinal(season, holdoutSeason) > 0)
            {
                goalsBook.Update(g.HomeTeam, g.AwayTeam, g.HomeScore!.Value, g.AwayScore!.Value);
                continue;
            }

            byOdds.TryGetValue(g.Id, out var snaps);
            snaps ??= [];
            var open = snaps.FirstOrDefault(s => s.PriceKind == PriceKind.Open);
            var close = snaps.FirstOrDefault(s => s.PriceKind == PriceKind.Close);
            if (open?.Line is not { } line || close?.Line is not { } closeLine) continue;
            if (Math.Abs(line - closeLine) < 0.25) continue;

            var modelExp = goalsBook.ExpectedTotal(g.HomeTeam, g.AwayTeam);
            var exp = marketWeight * line + (1 - marketWeight) * modelExp;
            var edge = exp - line;
            var acc = season == holdoutSeason ? hold : train;
            var juice = -110;
            var ou = TotalsMath.OuResult(g.HomeScore!.Value, g.AwayScore!.Value, line);
            var clv = TotalsMath.OverClvPoints(line, closeLine);
            var pOver = platt?.OverProb(modelExp, line) ?? TotalsMath.OverProb(exp, line, sharpness: 1.2);

            if (Math.Abs(edge) >= minAbsEdge)
            {
                if (edge > 0)
                {
                    var ev = OddsMath.ExpectedValue(pOver, juice);
                    if (ev >= minEv)
                        acc.AddBet(clv, ou > 0, TotalsMath.OuPnL(juice, ou), pOver);
                }
                else
                {
                    var pUnder = 1 - pOver;
                    var ev = OddsMath.ExpectedValue(pUnder, juice);
                    if (ev >= minEv)
                        acc.AddBet(-clv, ou < 0, TotalsMath.OuPnL(juice, -ou), pUnder);
                }
            }

            acc.AddGame();
            platt?.Observe(modelExp, line, ou);
            goalsBook.Update(g.HomeTeam, g.AwayTeam, g.HomeScore!.Value, g.AwayScore!.Value);
        }

        return (train.ToResult(), hold.ToResult());
    }

    public static async Task SweepAsync(
        string dbPath, string sport, string holdoutSeason, CancellationToken ct = default)
    {
        Console.WriteLine($"######## {sport} TOTALS (goals EMA) ########");
        Console.WriteLine($"Holdout locked: {holdoutSeason} (train earlier; skip later seasons)");
        var rows = new List<(double W, double Edge, Gate1aResult Train, Gate1aResult Hold)>();
        foreach (var w in new[] { 0.4, 0.5, 0.6, 0.7, 0.85 })
        foreach (var edge in new[] { 0.25, 0.35, 0.5, 0.75, 1.0 })
        {
            var (tr, ho) = await RunAsync(dbPath, sport, holdoutSeason, w, 0.04, edge, usePlatt: false, ct);
            rows.Add((w, edge, tr, ho));
            Console.WriteLine(
                $"w={w:F2} edge={edge:F2}  train n={tr.Bets} clv={tr.MeanClv:F2} pnl={tr.PaperPnlUnits:F1} | hold n={ho.Bets} clv={ho.MeanClv:F2} pnl={ho.PaperPnlUnits:F1} ll={ho.LogLoss:F4} br={ho.Brier:F4}");
        }

        var eligible = rows
            .Where(r => r.Train.MeanClv > 0 && r.Train.PaperPnlUnits > 0 && r.Train.Bets is >= 80 and <= 2000)
            .OrderByDescending(r => r.Train.PaperPnlUnits)
            .ToList();
        if (eligible.Count == 0)
        {
            Console.WriteLine($"KILL: HOLD FAIL — no {sport} train config in band.");
            return;
        }

        var best = eligible[0];
        Console.WriteLine($"=== {sport} CHAMP w={best.W:F2} edge={best.Edge:F2} ===");
        Console.WriteLine($"TRAIN  n={best.Train.Bets} clv={best.Train.MeanClv:F3} hit={best.Train.HitRate:P1} pnl={best.Train.PaperPnlUnits:F1}");
        Console.WriteLine(
            $"HOLDOUT n={best.Hold.Bets} clv={best.Hold.MeanClv:F3} hit={best.Hold.HitRate:P1} pnl={best.Hold.PaperPnlUnits:F1} ll={best.Hold.LogLoss:F4} br={best.Hold.Brier:F4}");
        var pass = best.Hold.PaperPnlUnits > 0 && best.Hold.MeanClv > 0 && best.Hold.Bets >= 40;
        Console.WriteLine(pass
            ? $"KILL: HOLD PASS — {sport} totals soft spot (paper)."
            : $"KILL: HOLD FAIL — {sport} not money yet.");

        if (sport == "LIGUE1")
            await ComparePlattAsync(dbPath, sport, holdoutSeason,
                Ligue1ChampConfig.MarketWeight, Ligue1ChampConfig.MinEv, Ligue1ChampConfig.MinAbsTotalEdge, ct);
        else
            await ComparePlattAsync(dbPath, sport, holdoutSeason, best.W, 0.04, best.Edge, ct);
    }

    private static async Task ComparePlattAsync(
        string dbPath, string sport, string holdoutSeason,
        double w, double minEv, double edge, CancellationToken ct)
    {
        var (_, baseHold) = await RunAsync(dbPath, sport, holdoutSeason, w, minEv, edge, usePlatt: false, ct);
        var (_, platHold) = await RunAsync(dbPath, sport, holdoutSeason, w, minEv, edge, usePlatt: true, ct);
        Console.WriteLine($"=== {sport} PLATT vs BASE @ w={w:F2} edge={edge:F2} ===");
        Console.WriteLine(
            $"BASE  n={baseHold.Bets} clv={baseHold.MeanClv:F3} pnl={baseHold.PaperPnlUnits:F1} ll={baseHold.LogLoss:F4} br={baseHold.Brier:F4}");
        Console.WriteLine(
            $"PLATT n={platHold.Bets} clv={platHold.MeanClv:F3} pnl={platHold.PaperPnlUnits:F1} ll={platHold.LogLoss:F4} br={platHold.Brier:F4}");
        var platPass = platHold.PaperPnlUnits > 0 && platHold.MeanClv > 0 && platHold.Bets >= 40;
        var beats = platPass
            && platHold.PaperPnlUnits > baseHold.PaperPnlUnits
            && platHold.MeanClv >= baseHold.MeanClv;
        Console.WriteLine(beats
            ? $"KILL: PLATT PASS — beats base hold CLV+PnL; promote {sport} ChampConfig."
            : $"KILL: PLATT FAIL — keep base champ (no {sport} ChampConfig change).");
    }

    private sealed class Acc(string slice)
    {
        private int _games, _bets, _hits;
        private double _clv, _pnl, _ll, _br;
        public void AddGame() => _games++;
        public void AddBet(double clv, bool won, double pnl, double pSide)
        {
            _bets++;
            _clv += clv;
            _pnl += pnl;
            if (won) _hits++;
            var p = Math.Clamp(pSide, 1e-6, 1 - 1e-6);
            var y = won ? 1.0 : 0.0;
            _ll += -(y * Math.Log(p) + (1 - y) * Math.Log(1 - p));
            _br += (p - y) * (p - y);
        }
        public Gate1aResult ToResult() => new()
        {
            Slice = slice, GamesScored = _games, Bets = _bets,
            MeanClv = _bets == 0 ? 0 : _clv / _bets, ClvUnit = "goals",
            HitRate = _bets == 0 ? 0 : (double)_hits / _bets, PaperPnlUnits = _pnl,
            LogLoss = _bets == 0 ? 0 : _ll / _bets,
            Brier = _bets == 0 ? 0 : _br / _bets,
        };
    }
}
