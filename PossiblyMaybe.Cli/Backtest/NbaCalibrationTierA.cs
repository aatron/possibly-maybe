using Microsoft.EntityFrameworkCore;
using PossiblyMaybe.Data;
using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Cli.Backtest;

/// <summary>
/// Round 5 T4b: one-shot Tier A for NBA B5 (<c>refs-nba</c>) and B7
/// (<c>+fitted-curve-nba</c>, <c>nba-strict</c>, EV-band) vs V0 on the same tickets.
/// Keep = mean fold CLV ≥ V0 and n≥40 → Shadow only; else FAIL marked. No paper / re-tune.
/// </summary>
public static class NbaCalibrationTierA
{
    public static readonly string[] FoldSeasons = NbaNormalSigmaTierA.FoldSeasons;

    /// <summary>B5 + B7 NBA cells scored once under BH with the rest of wave B.</summary>
    public static readonly string[] VariantIds =
    [
        "refs-nba",
        "V-FC-NBA",
        "V-NBA-STRICT",
        "V-EV-04-10",
        "V-EV-10-20",
        "V-EV-04-20",
    ];

    public const int NTrials = 6;

    public static async Task RunAsync(
        string dbPath,
        string? vaultDbPath = null,
        CancellationToken ct = default)
    {
        Console.WriteLine("######## Tier A once (T4b): NBA refs + calibration vs V0 ########");
        Console.WriteLine(
            $"V0 Normalσ PaceEff gates: w={ChampConfig.MarketWeight} |edge|≥{ChampConfig.MinAbsTotalEdge} EV≥{ChampConfig.MinEv:P0}");
        Console.WriteLine($"Variants: {string.Join(", ", VariantIds)}");
        Console.WriteLine($"N_trials={NTrials}. Folds: {string.Join(",", FoldSeasons)}");
        Console.WriteLine($"Holdout {TotalsEdgeBacktest.HoldoutSeason} not used for selection.");
        Console.WriteLine("PASS → Shadow only (PromotionPolicy). FAIL marked. No paper / re-tune.\n");

        var foldTable = await RunAllFoldsAsync(dbPath, ct);
        var v0ByFold = foldTable["V0"];

        PrintHeader();
        var summaries = new List<VariantSummary>();
        foreach (var id in VariantIds)
        {
            var byFold = foldTable[id];
            summaries.Add(Summarize(id, v0ByFold, byFold));
        }

        foreach (var s in summaries)
            PrintFoldRow(s);

        Console.WriteLine();
        foreach (var s in summaries)
            PrintSummary(s);

        var ledgerPath = vaultDbPath ?? VaultDb.DefaultPath(FindRepoRoot(dbPath));
        var trials = TrialLedger.Open(ledgerPath);
        var dateFrom = FoldSeasons[0];
        var dateTo = FoldSeasons[^1];
        foreach (var s in summaries)
        {
            var hash = Vault.StrategyHash(s.Id, "t4b-tier-a-once");
            trials.Record(hash, $"{{\"id\":\"{s.Id}\",\"tier\":\"A\",\"round\":\"5\",\"item\":\"T4b\"}}",
                dateFrom, dateTo, gridSize: 1);
        }

        Console.WriteLine();
        Console.WriteLine(trials.FormatReportFooter(observedSharpe: 0, nReturns: summaries.Sum(x => x.TotalBets)));
        Console.WriteLine($"Trial ledger: {ledgerPath} (+{NTrials} rows this run; N_trials cumulative={trials.NTrials})");
        Console.WriteLine("No paper-go / settle. OregonExclusion unchanged.");
    }

    /// <summary>
    /// Single walk-forward pass: score every fold season for V0 + T4b variants on the same O→C tickets.
    /// </summary>
    public static async Task<Dictionary<string, Dictionary<string, Gate1aResult>>> RunAllFoldsAsync(
        string dbPath,
        CancellationToken ct = default)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;
        await using var db = new AppDbContext(options);

        var games = (await db.Games.AsNoTracking()
                .Where(g => g.Sport == "NBA" && g.HomeScore != null && g.AwayScore != null)
                .ToListAsync(ct))
            .Where(g => string.CompareOrdinal(g.Season, TotalsEdgeBacktest.HoldoutSeason) < 0)
            .OrderBy(g => g.TipoffUtc).ThenBy(g => g.Id)
            .ToList();

        var odds = await db.OddsSnapshots.AsNoTracking()
            .Where(o => o.Market == "TOTAL")
            .ToListAsync(ct);
        var byGame = odds.GroupBy(o => o.GameId).ToDictionary(g => g.Key, g => g.ToList());

        Dictionary<int, GameBox> boxes = [];
        try { boxes = await db.GameBoxes.AsNoTracking().ToDictionaryAsync(b => b.GameId, ct); }
        catch { /* older DB */ }

        var refs = await (
            from r in db.RefereeAssignments.AsNoTracking()
            join g in db.Games.AsNoTracking() on r.GameId equals g.Id
            where g.Sport == "NBA"
            select r).ToListAsync(ct);
        var byRefs = refs.GroupBy(r => r.GameId).ToDictionary(g => g.Key, g => g.ToList());

        var foldSet = FoldSeasons.ToHashSet(StringComparer.Ordinal);
        var ids = new[] { "V0" }.Concat(VariantIds).ToArray();
        var accs = ids.ToDictionary(
            id => id,
            id => FoldSeasons.ToDictionary(s => s, s => new Acc($"{id}_{s}")),
            StringComparer.OrdinalIgnoreCase);

        var variants = ids.ToDictionary(
            id => id,
            id => id == "V0" ? VariantRegistry.V0 : VariantRegistry.Require(id),
            StringComparer.OrdinalIgnoreCase);

        var pace = new PaceEfficiencyBook();
        var resid = new ExpandingResidualSigma();
        var refFx = new RefEffects();

        foreach (var g in games)
        {
            byGame.TryGetValue(g.Id, out var snaps);
            snaps ??= [];
            byRefs.TryGetValue(g.Id, out var crew);
            crew ??= [];
            var open = snaps.FirstOrDefault(s => s.PriceKind == PriceKind.Open);
            var close = snaps.FirstOrDefault(s => s.PriceKind == PriceKind.Close);
            var modelExp = pace.ExpectedTotal(g.HomeTeam, g.AwayTeam);
            var names = crew.Select(c => c.OfficialName).ToList();
            var refResidual = refFx.CrewFoulPaceResidual(names);
            var excludeFit = crew.Any(x => x.DonaghyExcludeFromFit);

            // Drop garbage TOTAL opens (spreads / 10× typos) so CLV is comparable across arms.
            if (open?.Line is { } line && close?.Line is { } closeLine
                && line is >= 170 and <= 280 && closeLine is >= 170 and <= 280
                && Math.Abs(line - closeLine) >= 0.25
                && g.Season is not null
                && foldSet.Contains(g.Season))
            {
                var juice = open.AmericanOdds == 0 ? -110 : open.AmericanOdds;
                var ou = TotalsMath.OuResult(g.HomeScore!.Value, g.AwayScore!.Value, line);
                var clvOver = TotalsMath.OverClvPoints(line, closeLine);
                var day = g.TipoffUtc.UtcDateTime.ToString("yyyyMMdd");
                var cand = new SlateCandidate(
                    "NBA", day, g.HomeTeam, g.AwayTeam,
                    modelExp, null, null,
                    line, line, juice, juice,
                    resid.Sigma,
                    ChampConfig.MarketWeight, ChampConfig.MinAbsTotalEdge, ChampConfig.MinEv,
                    Stale: false,
                    RefTotalsResidual: refResidual);

                foreach (var id in ids)
                {
                    var v = variants[id];
                    var d = VariantRegistry.Evaluate(v, cand);
                    var acc = accs[id][g.Season];
                    // Same-ticket reliability: refs shift model; fitted-curve locks σ; filters share V0 P(over).
                    if (ou != 0)
                    {
                        var model = modelExp + (v.UseRefTotals ? refResidual : 0);
                        var blended = ChampConfig.MarketWeight * line
                                      + (1 - ChampConfig.MarketWeight) * model;
                        var sig = v.FittedCurve ? FittedCurves.NbaSigma : resid.Sigma;
                        acc.AddPred(TotalsMath.NormalOverProb(blended, line, sig), ou > 0);
                    }

                    if (d.Pick && d.Side is not null)
                    {
                        var betOver = d.Side.Equals("OVER", StringComparison.OrdinalIgnoreCase);
                        if (betOver)
                            acc.AddBet(clvOver, ou > 0, TotalsMath.OuPnL(juice, ou),
                                TotalsMath.OuPnL(juice, ou)
                                * OddsMath.FractionalKelly(d.ModelProb, juice, 0.25));
                        else
                            acc.AddBet(-clvOver, ou < 0, TotalsMath.OuPnL(juice, -ou),
                                TotalsMath.OuPnL(juice, -ou)
                                * OddsMath.FractionalKelly(d.ModelProb, juice, 0.25));
                    }

                    acc.AddGame();
                }
            }

            if (open?.Line is { } openLine && close?.Line is { } cl
                && openLine is >= 170 and <= 280 && cl is >= 170 and <= 280)
            {
                var blended = ChampConfig.MarketWeight * openLine
                              + (1 - ChampConfig.MarketWeight) * modelExp;
                resid.Observe(g.HomeScore!.Value + g.AwayScore!.Value - blended);
            }

            var totalPts = g.HomeScore!.Value + g.AwayScore!.Value;
            double? poss = null;
            if (boxes.TryGetValue(g.Id, out var box))
            {
                pace.Update(
                    g.HomeTeam, g.AwayTeam,
                    box.FgaHome, box.OrebHome, box.TovHome, box.FtaHome, box.PtsHome, box.Fg3aHome,
                    box.FgaAway, box.OrebAway, box.TovAway, box.FtaAway, box.PtsAway, box.Fg3aAway);
                poss = PaceEfficiencyBook.Possessions(box.FgaHome, box.OrebHome, box.TovHome, box.FtaHome)
                       + PaceEfficiencyBook.Possessions(box.FgaAway, box.OrebAway, box.TovAway, box.FtaAway);
            }

            refFx.ObserveTotal(names, totalPts, excludeFit, poss);
        }

        return ids.ToDictionary(
            id => id,
            id => FoldSeasons.ToDictionary(s => s, s => accs[id][s].ToResult()),
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>One mid fold smoke for tests (same path as full table).</summary>
    public static async Task<(Gate1aResult V0, Gate1aResult Refs, Gate1aResult Fitted)> RunFoldSmokeAsync(
        string dbPath, string testSeason, CancellationToken ct = default)
    {
        var all = await RunAllFoldsAsync(dbPath, ct);
        return (all["V0"][testSeason], all["refs-nba"][testSeason], all["V-FC-NBA"][testSeason]);
    }

    public static bool Keep(double meanClvVariant, double meanClvV0, int totalBets)
        => meanClvVariant >= meanClvV0 && totalBets >= 40;

    static VariantSummary Summarize(
        string id,
        Dictionary<string, Gate1aResult> v0,
        Dictionary<string, Gate1aResult> varFolds)
    {
        var nFolds = FoldSeasons.Length;
        double sumClv0 = 0, sumClvV = 0, sumPnl0 = 0, sumPnlV = 0, sumBr0 = 0, sumBrV = 0, sumLl0 = 0, sumLlV = 0;
        var n0 = 0;
        var nV = 0;
        var foldsClvWon = 0;
        var foldRows = new List<(string Season, Gate1aResult V0, Gate1aResult Var)>();

        foreach (var season in FoldSeasons)
        {
            var r0 = v0[season];
            var rV = varFolds[season];
            foldRows.Add((season, r0, rV));
            sumClv0 += r0.MeanClv;
            sumClvV += rV.MeanClv;
            sumPnl0 += r0.PaperPnlUnits;
            sumPnlV += rV.PaperPnlUnits;
            sumBr0 += r0.Brier;
            sumBrV += rV.Brier;
            sumLl0 += r0.LogLoss;
            sumLlV += rV.LogLoss;
            n0 += r0.Bets;
            nV += rV.Bets;
            if (rV.MeanClv >= r0.MeanClv) foldsClvWon++;
        }

        var mean0 = sumClv0 / nFolds;
        var meanV = sumClvV / nFolds;
        var pass = Keep(meanV, mean0, nV);
        LadderStage? shadow = null;
        if (pass)
            shadow = PromotionPolicy.EnterLiveShadow(tierAPass: true, vaultFinalPass: true);

        return new VariantSummary(
            id, foldRows, mean0, meanV, sumPnl0, sumPnlV,
            sumBr0 / nFolds, sumBrV / nFolds, sumLl0 / nFolds, sumLlV / nFolds,
            n0, nV, foldsClvWon, nFolds, pass, shadow);
    }

    static void PrintHeader()
    {
        Console.WriteLine(
            $"{"fold",6} {"nV0",5} {"clvV0",7} {"pnlV0",7} |" +
            $" {"variant",14} {"n",5} {"clv",7} {"pnl",7} {"dClv",7} {"br",7} {"ll",7}");
    }

    static void PrintFoldRow(VariantSummary s)
    {
        foreach (var (season, r0, rV) in s.Folds)
        {
            Console.WriteLine(
                $"{season,6} {r0.Bets,5} {r0.MeanClv,7:F2} {r0.PaperPnlUnits,7:F1} |" +
                $" {s.Id,14} {rV.Bets,5} {rV.MeanClv,7:F2} {rV.PaperPnlUnits,7:F1} " +
                $"{rV.MeanClv - r0.MeanClv,7:F2} {rV.Brier,7:F4} {rV.LogLoss,7:F4}");
        }
    }

    static void PrintSummary(VariantSummary s)
    {
        Console.WriteLine(
            $"SUMMARY {s.Id} meanClv V0={s.MeanClvV0:F3} var={s.MeanClvVar:F3} Δ={s.MeanClvVar - s.MeanClvV0:F3} " +
            $"pnl V0={s.SumPnlV0:F1} var={s.SumPnlVar:F1} n V0={s.TotalBetsV0} var={s.TotalBets} " +
            $"foldsClv≥V0={s.FoldsClvWon}/{s.NFolds} meanBr V0={s.MeanBrierV0:F4} var={s.MeanBrierVar:F4} " +
            $"meanLL V0={s.MeanLogLossV0:F4} var={s.MeanLogLossVar:F4}");
        if (s.Pass)
        {
            Console.WriteLine(
                $"VERDICT {s.Id} TierA=PASS → Shadow only (stage={s.Shadow}) — no paper promotion / re-tune");
        }
        else
        {
            Console.WriteLine(
                $"VERDICT {s.Id} TierA=FAIL marked (meanCLV≥V0={s.MeanClvVar >= s.MeanClvV0} n≥40={s.TotalBets >= 40}) — stays shadow");
        }
    }

    static string FindRepoRoot(string dbPath)
    {
        var dir = new DirectoryInfo(Path.GetDirectoryName(Path.GetFullPath(dbPath)) ?? ".");
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "PossiblyMaybe.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }

        return Directory.GetCurrentDirectory();
    }

    sealed record VariantSummary(
        string Id,
        IReadOnlyList<(string Season, Gate1aResult V0, Gate1aResult Var)> Folds,
        double MeanClvV0,
        double MeanClvVar,
        double SumPnlV0,
        double SumPnlVar,
        double MeanBrierV0,
        double MeanBrierVar,
        double MeanLogLossV0,
        double MeanLogLossVar,
        int TotalBetsV0,
        int TotalBets,
        int FoldsClvWon,
        int NFolds,
        bool Pass,
        LadderStage? Shadow);

    sealed class Acc(string slice)
    {
        private int _games, _bets, _hits, _nPred;
        private double _clv, _pnl, _kelly, _ll, _br;

        public void AddGame() => _games++;

        public void AddPred(double pOver, bool wentOver)
        {
            var p = Math.Clamp(pOver, 1e-6, 1 - 1e-6);
            var y = wentOver ? 1.0 : 0.0;
            _ll += -(y * Math.Log(p) + (1 - y) * Math.Log(1 - p));
            _br += (p - y) * (p - y);
            _nPred++;
        }

        public void AddBet(double clv, bool won, double pnl, double kellyPnl = 0)
        {
            _bets++;
            _clv += clv;
            _pnl += pnl;
            _kelly += kellyPnl;
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
            KellyQuarterPnl = _kelly,
        };
    }
}
