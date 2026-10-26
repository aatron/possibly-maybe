using Microsoft.EntityFrameworkCore;
using PossiblyMaybe.Data;
using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Cli.Backtest;

public enum TotalsModelKind
{
    /// <summary>Legacy: EMA points for/against.</summary>
    Scoring = 0,
    /// <summary>Pace × ORtg/DRtg from box (industry standard).</summary>
    PaceEff = 1,
    /// <summary>PaceEff + prior-season tracking eFG% adjustment.</summary>
    PaceEffEfg = 2,
}

/// <summary>
/// O/U walk-forward. Holdout = season 2025 (E 2024-25 open/close).
/// </summary>
public static class TotalsEdgeBacktest
{
    public const string HoldoutSeason = "2025";

    public static async Task<(Gate1aResult Train, Gate1aResult Hold)> RunAsync(
        string dbPath,
        double marketWeight,
        double minEv,
        double minAbsEdge,
        TotalsModelKind model = TotalsModelKind.PaceEff,
        string? trackingDir = null,
        CancellationToken ct = default)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;
        await using var db = new AppDbContext(options);

        var games = (await db.Games.AsNoTracking()
                .Where(g => g.Sport == "NBA" && g.HomeScore != null && g.AwayScore != null)
                .ToListAsync(ct))
            .OrderBy(g => g.TipoffUtc).ThenBy(g => g.Id)
            .ToList();

        var odds = await db.OddsSnapshots.AsNoTracking()
            .Where(o => o.Market == "TOTAL")
            .ToListAsync(ct);
        var byGame = odds.GroupBy(o => o.GameId).ToDictionary(g => g.Key, g => g.ToList());

        Dictionary<int, GameBox> boxes = [];
        try
        {
            boxes = await db.GameBoxes.AsNoTracking().ToDictionaryAsync(b => b.GameId, ct);
        }
        catch
        {
            // table may not exist yet on older DBs
        }

        var efgPriors = model == TotalsModelKind.PaceEffEfg
            ? TeamEfgPriors.Load(trackingDir ?? GuessTrackingDir(dbPath))
            : null;

        var scoring = new ScoringBook();
        var pace = new PaceEfficiencyBook();
        var train = new Acc("train_totals");
        var hold = new Acc("holdout_2024_25");
        var usedPace = 0;
        var usedScoringFallback = 0;
        var efgTouched = 0;

        foreach (var g in games)
        {
            byGame.TryGetValue(g.Id, out var snaps);
            snaps ??= [];
            var open = snaps.FirstOrDefault(s => s.PriceKind == PriceKind.Open);
            var close = snaps.FirstOrDefault(s => s.PriceKind == PriceKind.Close);

            // Ratings are pre-game; box row (if any) only updates the book after the bet decision.
            double modelExp = model == TotalsModelKind.Scoring
                ? scoring.ExpectedTotal(g.HomeTeam, g.AwayTeam)
                : pace.ExpectedTotal(g.HomeTeam, g.AwayTeam);
            if (model is TotalsModelKind.PaceEff or TotalsModelKind.PaceEffEfg)
            {
                if (boxes.ContainsKey(g.Id)) usedPace++;
                else usedScoringFallback++; // no box ⇒ pace book not updated this game (stale priors)
            }

            if (efgPriors is not null)
            {
                var adj = efgPriors.AdjPts(g.Season, g.HomeTeam, g.AwayTeam);
                if (adj != 0) efgTouched++;
                modelExp += adj;
            }

            var acc = g.Season == HoldoutSeason ? hold : train;

            if (open?.Line is { } line && close?.Line is { } closeLine
                && Math.Abs(line - closeLine) >= 0.25)
            {
                var exp = marketWeight * line + (1 - marketWeight) * modelExp;
                var edge = exp - line;
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
            if (boxes.TryGetValue(g.Id, out var box))
            {
                pace.Update(
                    g.HomeTeam, g.AwayTeam,
                    box.FgaHome, box.OrebHome, box.TovHome, box.FtaHome, box.PtsHome, box.Fg3aHome,
                    box.FgaAway, box.OrebAway, box.TovAway, box.FtaAway, box.PtsAway, box.Fg3aAway);
            }
        }

        Console.WriteLine(
            $"  [model={model} paceGamesTouched≈{usedPace} scoringFallback={usedScoringFallback} efgAdjGames={efgTouched}]");
        return (train.ToResult(), hold.ToResult());
    }

    private static string GuessTrackingDir(string dbPath)
    {
        var dir = new DirectoryInfo(Path.GetDirectoryName(Path.GetFullPath(dbPath)) ?? ".");
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "data", "raw", "nba_tracking");
            if (Directory.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        return Path.Combine("data", "raw", "nba_tracking");
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
