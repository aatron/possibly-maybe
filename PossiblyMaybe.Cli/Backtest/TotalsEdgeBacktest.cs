using Microsoft.EntityFrameworkCore;
using PossiblyMaybe.Data;
using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Cli.Backtest;

/// <summary>
/// O/U walk-forward: blend market open total with scoring EMA.
/// Holdout = season 2025 (E 2024-25 open/close); train = earlier B totals.
/// </summary>
public static class TotalsEdgeBacktest
{
    public const string HoldoutSeason = "2025";

    public static async Task<(Gate1aResult Train, Gate1aResult Hold)> RunAsync(
        string dbPath,
        double marketWeight,
        double minEv,
        double minAbsEdge,
        CancellationToken ct = default)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;
        await using var db = new AppDbContext(options);

        var games = (await db.Games.AsNoTracking()
                .Where(g => g.HomeScore != null && g.AwayScore != null)
                .ToListAsync(ct))
            .OrderBy(g => g.TipoffUtc).ThenBy(g => g.Id)
            .ToList();

        var odds = await db.OddsSnapshots.AsNoTracking()
            .Where(o => o.Market == "TOTAL")
            .ToListAsync(ct);
        var byGame = odds.GroupBy(o => o.GameId).ToDictionary(g => g.Key, g => g.ToList());

        var scoring = new ScoringBook();
        var train = new Acc("train_totals");
        var hold = new Acc("holdout_2024_25");

        foreach (var g in games)
        {
            byGame.TryGetValue(g.Id, out var snaps);
            snaps ??= [];
            var open = snaps.FirstOrDefault(s => s.PriceKind == PriceKind.Open);
            var close = snaps.FirstOrDefault(s => s.PriceKind == PriceKind.Close);
            var modelExp = scoring.ExpectedTotal(g.HomeTeam, g.AwayTeam);
            var acc = g.Season == HoldoutSeason ? hold : train;

            if (open?.Line is { } line && close?.Line is { } closeLine
                && Math.Abs(line - closeLine) >= 0.25)
            {
                var exp = marketWeight * line + (1 - marketWeight) * modelExp;
                var edge = exp - line; // >0 favors over
                var juice = open.AmericanOdds == 0 ? -110 : open.AmericanOdds;
                var ou = TotalsMath.OuResult(g.HomeScore!.Value, g.AwayScore!.Value, line);
                var clvOver = TotalsMath.OverClvPoints(line, closeLine);

                if (Math.Abs(edge) >= minAbsEdge)
                {
                    if (edge > 0)
                    {
                        var p = TotalsMath.OverProb(exp, line);
                        var ev = OddsMath.ExpectedValue(p, juice);
                        if (ev >= minEv)
                            acc.AddBet(clvOver, ou > 0, TotalsMath.OuPnL(juice, ou));
                    }
                    else
                    {
                        var p = 1 - TotalsMath.OverProb(exp, line);
                        var ev = OddsMath.ExpectedValue(p, juice);
                        if (ev >= minEv)
                            acc.AddBet(-clvOver, ou < 0, TotalsMath.OuPnL(juice, -ou));
                    }
                }

                acc.AddGame();
            }

            scoring.Update(g.HomeTeam, g.AwayTeam, g.HomeScore!.Value, g.AwayScore!.Value);
        }

        return (train.ToResult(), hold.ToResult());
    }

    private sealed class Acc(string slice)
    {
        private int _games, _bets, _hits;
        private double _clv, _pnl;

        public void AddGame() => _games++;

        public void AddBet(double clv, bool won, double pnl)
        {
            _bets++;
            _clv += clv;
            _pnl += pnl;
            if (won) _hits++;
        }

        public Gate1aResult ToResult() => new()
        {
            Slice = slice,
            GamesScored = _games,
            Bets = _bets,
            MeanClv = _bets == 0 ? 0 : _clv / _bets,
            ClvUnit = "total_pts",
            HitRate = _bets == 0 ? 0 : (double)_hits / _bets,
            PaperPnlUnits = _pnl,
        };
    }
}
