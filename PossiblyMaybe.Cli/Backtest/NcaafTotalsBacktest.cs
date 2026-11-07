using Microsoft.EntityFrameworkCore;
using PossiblyMaybe.Data;
using PossiblyMaybe.Domain;
using PossiblyMaybe.Domain.SportPacks;

namespace PossiblyMaybe.Cli.Backtest;

/// <summary>
/// NCAAF totals: ScoringBook EMA vs SBRO open, Oregon sides skipped.
/// Holdout from <see cref="NcaafSportPack"/> (train ≤2021-22, Tier C 2022-23 locked first).
/// </summary>
public static class NcaafTotalsBacktest
{
    public static string HoldoutSeason => NcaafSportPack.Instance.TierCHoldoutSeason;
    public static string TrainMaxSeason => NcaafSportPack.Instance.TrainMaxSeason;

    public static async Task<(Gate1aResult Train, Gate1aResult Hold)> RunAsync(
        string dbPath,
        double marketWeight = NcaafChampConfig.MarketWeight,
        double minEv = NcaafChampConfig.MinEv,
        double minAbsEdge = NcaafChampConfig.MinAbsTotalEdge,
        double alpha = NcaafChampConfig.EmaAlpha,
        CancellationToken ct = default)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={dbPath}").Options;
        await using var db = new AppDbContext(options);

        var games = (await db.Games.AsNoTracking()
                .Where(g => g.Sport == "NCAAF" && g.HomeScore != null)
                .ToListAsync(ct))
            .OrderBy(g => g.TipoffUtc).ThenBy(g => g.Id)
            .ToList();
        var odds = await db.OddsSnapshots.AsNoTracking().Where(o => o.Market == "TOTAL").ToListAsync(ct);
        var byOdds = odds.GroupBy(o => o.GameId).ToDictionary(g => g.Key, g => g.ToList());

        var scoring = new ScoringBook(alpha: alpha, leagueInit: NcaafChampConfig.LeagueInit);
        var train = new Acc("ncaaf_train");
        var hold = new Acc($"ncaaf_holdout_{HoldoutSeason}");

        foreach (var g in games)
        {
            var season = g.Season ?? "";
            // Skip seasons after locked holdout (no CFBD yet); still update books below when scored.
            if (string.CompareOrdinal(season, HoldoutSeason) > 0)
            {
                scoring.Update(g.HomeTeam, g.AwayTeam, g.HomeScore!.Value, g.AwayScore!.Value);
                continue;
            }

            byOdds.TryGetValue(g.Id, out var snaps);
            snaps ??= [];
            var open = snaps.FirstOrDefault(s => s.PriceKind == PriceKind.Open);
            var close = snaps.FirstOrDefault(s => s.PriceKind == PriceKind.Close);
            if (open?.Line is not { } betLine || close?.Line is not { } closeLine
                || open.ClvQuality != ClvQuality.OpenClose)
            {
                scoring.Update(g.HomeTeam, g.AwayTeam, g.HomeScore!.Value, g.AwayScore!.Value);
                continue;
            }

            var acc = season == HoldoutSeason ? hold : train;
            acc.AddGame();

            // Cardinal rule — never bet either Oregon side
            if (OregonExclusion.IsExcluded("NCAAF", g.HomeTeam, g.AwayTeam))
            {
                scoring.Update(g.HomeTeam, g.AwayTeam, g.HomeScore!.Value, g.AwayScore!.Value);
                continue;
            }

            var modelExp = scoring.ExpectedTotal(g.HomeTeam, g.AwayTeam);
            var exp = marketWeight * betLine + (1 - marketWeight) * modelExp;
            var edge = exp - betLine;
            var juice = -110;
            var ou = TotalsMath.OuResult(g.HomeScore!.Value, g.AwayScore!.Value, betLine);
            var hasClv = Math.Abs(betLine - closeLine) >= 0.25;
            var clv = hasClv ? TotalsMath.OverClvPoints(betLine, closeLine) : 0;

            if (Math.Abs(edge) >= minAbsEdge)
            {
                if (edge > 0)
                {
                    var p = TotalsMath.OverProb(exp, betLine, sharpness: 0.2);
                    var ev = OddsMath.ExpectedValue(p, juice);
                    if (ev >= minEv)
                        acc.AddBet(clv, ou > 0, TotalsMath.OuPnL(juice, ou), hasClv);
                }
                else
                {
                    var p = 1 - TotalsMath.OverProb(exp, betLine, sharpness: 0.2);
                    var ev = OddsMath.ExpectedValue(p, juice);
                    if (ev >= minEv)
                        acc.AddBet(hasClv ? -clv : 0, ou < 0, TotalsMath.OuPnL(juice, -ou), hasClv);
                }
            }

            scoring.Update(g.HomeTeam, g.AwayTeam, g.HomeScore!.Value, g.AwayScore!.Value);
        }

        return (train.ToResult(), hold.ToResult());
    }

    /// <summary>Tier A: leave-one-season-out on pre-holdout seasons only (fixed provisional gates).</summary>
    public static async Task WalkForwardTierAAsync(string dbPath, CancellationToken ct = default)
    {
        var pack = NcaafSportPack.Instance;
        Console.WriteLine("######## NCAAF Tier A walk-forward (pre-holdout) ########");
        Console.WriteLine(
            $"Gates locked provisional: w={pack.MarketWeight:F2} edge={pack.MinAbsTotalEdge:F1} ev≥{pack.MinEv:P0} α={pack.EmaAlpha:F2}");
        Console.WriteLine($"Tier C holdout locked (not scored here): {HoldoutSeason}");

        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={dbPath}").Options;
        await using var db = new AppDbContext(options);
        var games = (await db.Games.AsNoTracking()
                .Where(g => g.Sport == "NCAAF" && g.HomeScore != null && g.Season != null)
                .ToListAsync(ct))
            .Where(g => string.CompareOrdinal(g.Season, HoldoutSeason) < 0)
            .OrderBy(g => g.TipoffUtc).ThenBy(g => g.Id)
            .ToList();
        var odds = await db.OddsSnapshots.AsNoTracking().Where(o => o.Market == "TOTAL").ToListAsync(ct);
        var byOdds = odds.GroupBy(o => o.GameId).ToDictionary(g => g.Key, g => g.ToList());

        var folds = pack.TierAFolds.Where(s => games.Any(g => g.Season == s)).ToList();
        foreach (var fold in folds)
        {
            var scoring = new ScoringBook(alpha: pack.EmaAlpha, leagueInit: NcaafChampConfig.LeagueInit);
            var acc = new Acc($"ncaaf_fold_{fold}");
            foreach (var g in games)
            {
                byOdds.TryGetValue(g.Id, out var snaps);
                snaps ??= [];
                var open = snaps.FirstOrDefault(s => s.PriceKind == PriceKind.Open);
                var close = snaps.FirstOrDefault(s => s.PriceKind == PriceKind.Close);
                var season = g.Season!;
                var isFold = season == fold;

                if (open?.Line is not { } betLine || close?.Line is not { } closeLine
                    || open.ClvQuality != ClvQuality.OpenClose)
                {
                    scoring.Update(g.HomeTeam, g.AwayTeam, g.HomeScore!.Value, g.AwayScore!.Value);
                    continue;
                }

                if (isFold)
                {
                    acc.AddGame();
                    if (!OregonExclusion.IsExcluded("NCAAF", g.HomeTeam, g.AwayTeam))
                    {
                        var modelExp = scoring.ExpectedTotal(g.HomeTeam, g.AwayTeam);
                        var exp = pack.MarketWeight * betLine + (1 - pack.MarketWeight) * modelExp;
                        var edge = exp - betLine;
                        var juice = -110;
                        var ou = TotalsMath.OuResult(g.HomeScore!.Value, g.AwayScore!.Value, betLine);
                        var hasClv = Math.Abs(betLine - closeLine) >= 0.25;
                        var clv = hasClv ? TotalsMath.OverClvPoints(betLine, closeLine) : 0;
                        if (Math.Abs(edge) >= pack.MinAbsTotalEdge)
                        {
                            if (edge > 0)
                            {
                                var p = TotalsMath.OverProb(exp, betLine, sharpness: 0.2);
                                if (OddsMath.ExpectedValue(p, juice) >= pack.MinEv)
                                    acc.AddBet(clv, ou > 0, TotalsMath.OuPnL(juice, ou), hasClv);
                            }
                            else
                            {
                                var p = 1 - TotalsMath.OverProb(exp, betLine, sharpness: 0.2);
                                if (OddsMath.ExpectedValue(p, juice) >= pack.MinEv)
                                    acc.AddBet(hasClv ? -clv : 0, ou < 0, TotalsMath.OuPnL(juice, -ou), hasClv);
                            }
                        }
                    }
                }

                scoring.Update(g.HomeTeam, g.AwayTeam, g.HomeScore!.Value, g.AwayScore!.Value);
            }

            var r = acc.ToResult();
            Console.WriteLine(
                $"fold={fold} n={r.Bets} clv={r.MeanClv:F3} hit={r.HitRate:P1} pnl={r.PaperPnlUnits:F1}");
        }
    }

    public static async Task SweepAsync(string dbPath, CancellationToken ct = default)
    {
        Console.WriteLine("######## NCAAF TOTALS (Scoring EMA + SBRO O→C) ########");
        Console.WriteLine(
            $"Holdout season locked BEFORE tuning: {HoldoutSeason} (train ≤{TrainMaxSeason}; skip ≥{NcaafSportPack.Instance.SkipSeason})");
        Console.WriteLine("Oregon / Oregon St / Portland St excluded at bet time.");

        await WalkForwardTierAAsync(dbPath, ct);

        // Train-only grid; holdout scored but never used to pick champ.
        var rows = new List<(double W, double Edge, Gate1aResult Train, Gate1aResult Hold)>();
        foreach (var w in new[] { 0.5, 0.6, 0.7, 0.85 })
        foreach (var edge in new[] { 3.0, 4.0, 5.0, 6.0 })
        {
            var (tr, ho) = await RunAsync(dbPath, w, NcaafChampConfig.MinEv, edge, ct: ct);
            rows.Add((w, edge, tr, ho));
            Console.WriteLine(
                $"w={w:F2} edge={edge:F1}  train n={tr.Bets} clv={tr.MeanClv:F2} pnl={tr.PaperPnlUnits:F1} | hold n={ho.Bets} clv={ho.MeanClv:F2} pnl={ho.PaperPnlUnits:F1}");
        }

        var eligible = rows
            .Where(r => r.Train.MeanClv > 0 && r.Train.PaperPnlUnits > 0 && r.Train.Bets is >= 80 and <= 4000)
            .OrderByDescending(r => r.Train.PaperPnlUnits)
            .ToList();
        if (eligible.Count == 0)
        {
            Console.WriteLine("KILL: HOLD FAIL — no NCAAF train config in band.");
            // Still report provisional champ look-once
            var (tr0, ho0) = await RunAsync(dbPath, ct: ct);
            Console.WriteLine(
                $"PROVISIONAL (config defaults) train n={tr0.Bets} clv={tr0.MeanClv:F3} pnl={tr0.PaperPnlUnits:F1} | hold n={ho0.Bets} clv={ho0.MeanClv:F3} pnl={ho0.PaperPnlUnits:F1}");
            return;
        }

        var best = eligible[0];
        Console.WriteLine($"=== NCAAF train-max (holdout look-once only) w={best.W:F2} edge={best.Edge:F1} ===");
        Console.WriteLine($"TRAIN  n={best.Train.Bets} clv={best.Train.MeanClv:F3} hit={best.Train.HitRate:P1} pnl={best.Train.PaperPnlUnits:F1}");
        Console.WriteLine($"HOLDOUT n={best.Hold.Bets} clv={best.Hold.MeanClv:F3} hit={best.Hold.HitRate:P1} pnl={best.Hold.PaperPnlUnits:F1}");
        var pass = best.Hold.PaperPnlUnits > 0 && best.Hold.MeanClv > 0 && best.Hold.Bets >= 40;
        Console.WriteLine(pass
            ? "KILL: HOLD PASS — NCAAF totals (shadow candidate)."
            : "KILL: HOLD FAIL — NCAAF not money yet; keep shadow through bowls.");
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
