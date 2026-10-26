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
    /// <summary>PaceEff + walk-forward Platt on blended edge → P(over).</summary>
    PaceEffPlatt = 3,
    /// <summary>PaceEff + walk-forward ridge-logistic on [line, modelExp] → P(over).</summary>
    PaceEffRidge = 4,
}

/// <summary>
/// O/U walk-forward. Holdout = season 2025 (E 2024-25 open/close).
/// </summary>
public static class TotalsEdgeBacktest
{
    public const string HoldoutSeason = "2025";
    private const int RidgeMinRows = 100;
    private const double RidgeLambda = 1.0;
    private const int RidgeRefitEvery = 50; // shortcut: not every tip; upgrade if cal drifts

    public static async Task<(Gate1aResult Train, Gate1aResult Hold)> RunAsync(
        string dbPath,
        double marketWeight,
        double minEv,
        double minAbsEdge,
        TotalsModelKind model = TotalsModelKind.PaceEff,
        string? trackingDir = null,
        double alpha = 0.15,
        double restB2bPts = 0,
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

        var scoring = new ScoringBook(alpha: alpha);
        var pace = new PaceEfficiencyBook(alpha: alpha);
        var rest = new RestTracker();
        var train = new Acc("train_totals");
        var hold = new Acc("holdout_2024_25");
        var usedPace = 0;
        var usedScoringFallback = 0;
        var efgTouched = 0;

        OuPlattCalibrator? platt = model == TotalsModelKind.PaceEffPlatt
            ? new OuPlattCalibrator(fallbackSharpness: 0.25)
            : null;
        var ridgeX = model == TotalsModelKind.PaceEffRidge ? new List<double[]>() : null;
        var ridgeY = model == TotalsModelKind.PaceEffRidge ? new List<double>() : null;
        double[]? ridgeBeta = null;
        var ridgeFitAt = 0;

        foreach (var g in games)
        {
            byGame.TryGetValue(g.Id, out var snaps);
            snaps ??= [];
            var open = snaps.FirstOrDefault(s => s.PriceKind == PriceKind.Open);
            var close = snaps.FirstOrDefault(s => s.PriceKind == PriceKind.Close);
            var date = DateOnly.FromDateTime(g.TipoffUtc.UtcDateTime);

            // Ratings are pre-game; box row (if any) only updates the book after the bet decision.
            double modelExp = model == TotalsModelKind.Scoring
                ? scoring.ExpectedTotal(g.HomeTeam, g.AwayTeam)
                : pace.ExpectedTotal(g.HomeTeam, g.AwayTeam);
            if (restB2bPts != 0)
                modelExp += restB2bPts * rest.B2bCount(g.HomeTeam, g.AwayTeam, date);

            if (model is TotalsModelKind.PaceEff or TotalsModelKind.PaceEffEfg
                or TotalsModelKind.PaceEffPlatt or TotalsModelKind.PaceEffRidge)
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

                var pOver = PredictOver(platt, ridgeX, ridgeY, ref ridgeBeta, ref ridgeFitAt,
                    exp, line, modelExp);

                // Secondary ISLP metrics on all scored non-push games.
                if (ou != 0)
                    acc.AddPred(pOver, ou > 0);

                if (Math.Abs(edge) >= minAbsEdge)
                {
                    if (edge > 0)
                    {
                        var ev = OddsMath.ExpectedValue(pOver, juice);
                        if (ev >= minEv)
                            acc.AddBet(clvOver, ou > 0, TotalsMath.OuPnL(juice, ou));
                    }
                    else
                    {
                        var pUnder = 1 - pOver;
                        var ev = OddsMath.ExpectedValue(pUnder, juice);
                        if (ev >= minEv)
                            acc.AddBet(-clvOver, ou < 0, TotalsMath.OuPnL(juice, -ou));
                    }
                }

                acc.AddGame();

                // Leakage-safe: update calibrator only after this tip's decision.
                platt?.Observe(exp, line, ou);
                if (ridgeX is not null && ridgeY is not null && ou != 0)
                {
                    ridgeX.Add([1.0, line / 100.0, modelExp / 100.0]);
                    ridgeY.Add(ou > 0 ? 1.0 : 0.0);
                }
            }

            scoring.Update(g.HomeTeam, g.AwayTeam, g.HomeScore!.Value, g.AwayScore!.Value);
            if (boxes.TryGetValue(g.Id, out var box))
            {
                pace.Update(
                    g.HomeTeam, g.AwayTeam,
                    box.FgaHome, box.OrebHome, box.TovHome, box.FtaHome, box.PtsHome, box.Fg3aHome,
                    box.FgaAway, box.OrebAway, box.TovAway, box.FtaAway, box.PtsAway, box.Fg3aAway);
            }
            rest.Played(g.HomeTeam, date);
            rest.Played(g.AwayTeam, date);
        }

        Console.WriteLine(
            $"  [model={model} α={alpha:F3} restB2bPts={restB2bPts:F2} paceGamesTouched≈{usedPace} scoringFallback={usedScoringFallback} efgAdjGames={efgTouched}]");
        return (train.ToResult(), hold.ToResult());
    }

    /// <summary>
    /// Soft-threshold OLS of (actual−modelExp) on B2B-count using TRAIN seasons only.
    /// Returns 0 if |β| &lt; thresh (L1-style drop).
    /// </summary>
    public static async Task<double> FitRestB2bPtsAsync(
        string dbPath, TotalsModelKind model, double alpha, double softThreshPts = 0.5,
        CancellationToken ct = default)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;
        await using var db = new AppDbContext(options);
        var games = (await db.Games.AsNoTracking()
                .Where(g => g.Sport == "NBA" && g.HomeScore != null && g.Season != HoldoutSeason)
                .ToListAsync(ct))
            .OrderBy(g => g.TipoffUtc).ThenBy(g => g.Id)
            .ToList();
        Dictionary<int, GameBox> boxes = [];
        try { boxes = await db.GameBoxes.AsNoTracking().ToDictionaryAsync(b => b.GameId, ct); }
        catch { /* older DB */ }

        var scoring = new ScoringBook(alpha: alpha);
        var pace = new PaceEfficiencyBook(alpha: alpha);
        var rest = new RestTracker();
        double sxx = 0, sxy = 0;
        foreach (var g in games)
        {
            var date = DateOnly.FromDateTime(g.TipoffUtc.UtcDateTime);
            var modelExp = model == TotalsModelKind.Scoring
                ? scoring.ExpectedTotal(g.HomeTeam, g.AwayTeam)
                : pace.ExpectedTotal(g.HomeTeam, g.AwayTeam);
            var x = (double)rest.B2bCount(g.HomeTeam, g.AwayTeam, date);
            var y = g.HomeScore!.Value + g.AwayScore!.Value - modelExp;
            sxx += x * x;
            sxy += x * y;
            scoring.Update(g.HomeTeam, g.AwayTeam, g.HomeScore.Value, g.AwayScore!.Value);
            if (boxes.TryGetValue(g.Id, out var box))
            {
                pace.Update(
                    g.HomeTeam, g.AwayTeam,
                    box.FgaHome, box.OrebHome, box.TovHome, box.FtaHome, box.PtsHome, box.Fg3aHome,
                    box.FgaAway, box.OrebAway, box.TovAway, box.FtaAway, box.PtsAway, box.Fg3aAway);
            }
            rest.Played(g.HomeTeam, date);
            rest.Played(g.AwayTeam, date);
        }

        if (sxx < 1e-9) return 0;
        var beta = sxy / sxx;
        return Math.Abs(beta) < softThreshPts ? 0 : beta;
    }

    private static double PredictOver(
        OuPlattCalibrator? platt,
        List<double[]>? ridgeX,
        List<double>? ridgeY,
        ref double[]? ridgeBeta,
        ref int ridgeFitAt,
        double exp,
        double line,
        double modelExp)
    {
        if (platt is not null)
            return platt.OverProb(exp, line);

        if (ridgeX is null || ridgeY is null || ridgeX.Count < RidgeMinRows)
            return TotalsMath.OverProb(exp, line);

        if (ridgeBeta is null || ridgeX.Count - ridgeFitAt >= RidgeRefitEvery)
        {
            ridgeBeta = RidgeLogistic.Fit(ridgeX, ridgeY, RidgeLambda, warm: ridgeBeta);
            ridgeFitAt = ridgeX.Count;
        }

        return RidgeLogistic.Predict(ridgeBeta, [1.0, line / 100.0, modelExp / 100.0]);
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
        private int _games, _bets, _hits, _nPred;
        private double _clv, _pnl, _ll, _br;

        public void AddGame() => _games++;

        public void AddPred(double pOver, bool wentOver)
        {
            var p = Math.Clamp(pOver, 1e-6, 1 - 1e-6);
            var y = wentOver ? 1.0 : 0.0;
            _ll += -(y * Math.Log(p) + (1 - y) * Math.Log(1 - p));
            _br += (p - y) * (p - y);
            _nPred++;
        }

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
            LogLoss = _nPred == 0 ? 0 : _ll / _nPred,
            Brier = _nPred == 0 ? 0 : _br / _nPred,
            PaperPnlUnits = _pnl,
        };
    }
}
