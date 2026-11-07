using Microsoft.EntityFrameworkCore;
using PossiblyMaybe.Data;
using PossiblyMaybe.Domain;
using PossiblyMaybe.Domain.SportPacks;

namespace PossiblyMaybe.Cli.Backtest;

/// <summary>
/// NCAAB totals: PaceEfficiencyBook when GameBox exists, else ScoringBook (points EMA).
/// Holdout from <see cref="NcaabSportPack"/> — locked before tuning (2021-22).
/// Oregon sides are never bet (cardinal rule).
/// </summary>
public static class NcaabTotalsBacktest
{
    public static string HoldoutSeason => NcaabSportPack.Instance.TierCHoldoutSeason;

    public static async Task<(Gate1aResult Train, Gate1aResult Hold)> RunAsync(
        string dbPath, double marketWeight, double minEv, double minAbsEdge,
        double alpha = NcaabChampConfig.EmaAlpha, string? holdoutSeason = null,
        CancellationToken ct = default)
    {
        holdoutSeason ??= HoldoutSeason;
        var pack = NcaabSportPack.Instance;
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={dbPath}").Options;
        await using var db = new AppDbContext(options);

        var games = (await db.Games.AsNoTracking()
                .Where(g => g.Sport == "NCAAB" && g.HomeScore != null && g.AwayScore != null)
                .ToListAsync(ct))
            .OrderBy(g => g.TipoffUtc).ThenBy(g => g.Id)
            .ToList();
        var odds = await db.OddsSnapshots.AsNoTracking().Where(o => o.Market == "TOTAL").ToListAsync(ct);
        var byOdds = odds.GroupBy(o => o.GameId).ToDictionary(g => g.Key, g => g.ToList());
        Dictionary<int, GameBox> boxes = [];
        try { boxes = await db.GameBoxes.AsNoTracking().ToDictionaryAsync(b => b.GameId, ct); }
        catch { /* NCAAB boxes usually absent */ }

        var scoring = new ScoringBook(alpha: alpha, leagueInit: NcaabChampConfig.LeagueInitTotal);
        var pace = new PaceEfficiencyBook(alpha: alpha, initPace: 68, initOrtg: 105);
        var train = new Acc("ncaab_train");
        var hold = new Acc($"ncaab_holdout_{holdoutSeason}");

        foreach (var g in games)
        {
            // Seasons after holdout / skip label: update books only (never score Tier C+ as train).
            if (string.CompareOrdinal(g.Season, holdoutSeason) > 0
                || string.CompareOrdinal(g.Season, pack.SkipSeason) >= 0)
            {
                ApplyUpdate(g, boxes, scoring, pace);
                continue;
            }

            byOdds.TryGetValue(g.Id, out var snaps);
            snaps ??= [];
            var open = snaps.FirstOrDefault(s => s.PriceKind == PriceKind.Open);
            var close = snaps.FirstOrDefault(s => s.PriceKind == PriceKind.Close);
            if (open?.Line is not { } line || close?.Line is not { } closeLine)
            {
                ApplyUpdate(g, boxes, scoring, pace);
                continue;
            }
            if (Math.Abs(line - closeLine) < 0.25)
            {
                ApplyUpdate(g, boxes, scoring, pace);
                continue;
            }

            var modelExp = boxes.ContainsKey(g.Id)
                ? pace.ExpectedTotal(g.HomeTeam, g.AwayTeam)
                : scoring.ExpectedTotal(g.HomeTeam, g.AwayTeam);
            var exp = marketWeight * line + (1 - marketWeight) * modelExp;
            var edge = exp - line;
            var acc = g.Season == holdoutSeason ? hold : train;
            var juice = -110;
            var ou = TotalsMath.OuResult(g.HomeScore!.Value, g.AwayScore!.Value, line);
            var clv = TotalsMath.OverClvPoints(line, closeLine);
            var pOver = TotalsMath.OverProb(exp, line, sharpness: 0.2);

            // Cardinal rule: never bet Oregon-based sides (still update ratings).
            if (!OregonExclusion.IsExcluded("NCAAB", g.HomeTeam, g.AwayTeam)
                && Math.Abs(edge) >= minAbsEdge)
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
            ApplyUpdate(g, boxes, scoring, pace);
        }

        return (train.ToResult(), hold.ToResult());
    }

    static void ApplyUpdate(Game g, Dictionary<int, GameBox> boxes, ScoringBook scoring, PaceEfficiencyBook pace)
    {
        scoring.Update(g.HomeTeam, g.AwayTeam, g.HomeScore!.Value, g.AwayScore!.Value);
        if (!boxes.TryGetValue(g.Id, out var b)) return;
        pace.Update(g.HomeTeam, g.AwayTeam,
            b.FgaHome, b.OrebHome, b.TovHome, b.FtaHome, b.PtsHome, b.Fg3aHome,
            b.FgaAway, b.OrebAway, b.TovAway, b.FtaAway, b.PtsAway, b.Fg3aAway);
    }

    public static async Task SweepAsync(string dbPath, CancellationToken ct = default)
    {
        var pack = NcaabSportPack.Instance;
        Console.WriteLine("######## NCAAB TOTALS (PaceEff / ScoringBook fallback) ########");
        Console.WriteLine(
            $"Holdout season LOCKED before tuning: {HoldoutSeason} (train ≤{pack.TrainMaxSeason}; skip ≥{pack.SkipSeason})");
        var rows = new List<(double W, double Edge, Gate1aResult Train, Gate1aResult Hold)>();
        foreach (var w in new[] { 0.4, 0.5, 0.6, 0.7 })
        foreach (var edge in new[] { 3.0, 4.0, 5.0, 6.0, 8.0 })
        {
            var (tr, ho) = await RunAsync(dbPath, w, 0.04, edge, ct: ct);
            rows.Add((w, edge, tr, ho));
            Console.WriteLine(
                $"w={w:F2} edge={edge:F1}  train n={tr.Bets} clv={tr.MeanClv:F2} pnl={tr.PaperPnlUnits:F1} | hold n={ho.Bets} clv={ho.MeanClv:F2} pnl={ho.PaperPnlUnits:F1}");
        }

        var eligible = rows
            .Where(r => r.Train.MeanClv > 0 && r.Train.PaperPnlUnits > 0 && r.Train.Bets is >= 80 and <= 8000)
            .OrderByDescending(r => r.Train.PaperPnlUnits)
            .ToList();
        if (eligible.Count == 0)
        {
            Console.WriteLine("KILL: HOLD FAIL — no NCAAB train config in band.");
            return;
        }

        var best = eligible[0];
        Console.WriteLine($"=== NCAAB CHAMP (train max PnL; holdout look-once) w={best.W:F2} edge={best.Edge:F1} ===");
        Console.WriteLine($"TRAIN  n={best.Train.Bets} clv={best.Train.MeanClv:F3} hit={best.Train.HitRate:P1} pnl={best.Train.PaperPnlUnits:F1}");
        Console.WriteLine(
            $"HOLDOUT n={best.Hold.Bets} clv={best.Hold.MeanClv:F3} hit={best.Hold.HitRate:P1} pnl={best.Hold.PaperPnlUnits:F1}");
        var pass = best.Hold.PaperPnlUnits > 0 && best.Hold.MeanClv > 0 && best.Hold.Bets >= 40;
        Console.WriteLine(pass
            ? "KILL: HOLD PASS — NCAAB totals soft spot (shadow OK; paper only after wave radar)."
            : "KILL: HOLD FAIL — NCAAB not money yet (remain shadow-only).");
        Console.WriteLine(
            $"NOTE: provisional NcaabChampConfig stays w={NcaabChampConfig.MarketWeight} edge={NcaabChampConfig.MinAbsTotalEdge} until a deliberate promote.");
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
            MeanClv = _bets == 0 ? 0 : _clv / _bets, ClvUnit = "pts",
            HitRate = _bets == 0 ? 0 : (double)_hits / _bets, PaperPnlUnits = _pnl,
            LogLoss = _bets == 0 ? 0 : _ll / _bets,
            Brier = _bets == 0 ? 0 : _br / _bets,
        };
    }
}
