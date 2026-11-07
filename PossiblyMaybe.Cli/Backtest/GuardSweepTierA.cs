using Microsoft.EntityFrameworkCore;
using PossiblyMaybe.Data;
using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Cli.Backtest;

/// <summary>
/// Tier A guard selection: edge caps {20,25,30,35}% (N_trials=4) and line-move k.
/// Proof: removed bets have negative PnL or mean CLV in a majority of folds.
/// </summary>
public static class GuardSweepTierA
{
    public static readonly double[] EdgeCaps = [0.20, 0.25, 0.30, 0.35];
    public static readonly double[] NbaLineMoveKs = [1.5, 2.0];
    public const double SoccerLineMoveK = 0.5;
    public const int NTrialsEdge = 4;

    public readonly record struct CapChoice(double Cap, int NegFolds, int FoldsWithRemovals, int TotalFolds, double RemovedPnl, int RemovedBets);
    public readonly record struct KChoice(double K, int NegFolds, int FoldsWithRemovals, int TotalFolds, double RemovedPnl, int RemovedBets);

    public static async Task<CapChoice> RunAsync(string dbPath, CancellationToken ct = default)
    {
        Console.WriteLine("######## Tier A: blindside guards (edge cap + line-move) ########");
        Console.WriteLine($"Edge caps={{20,25,30,35}}% N_trials={NTrialsEdge}; NBA k∈{{1.5,2}}; soccer k={SoccerLineMoveK} or strike≥8¢");
        Console.WriteLine("Proof: removed bets PnL<0 or meanCLV<0 in a majority of folds with removals.\n");

        var nbaCap = await SweepNbaEdgeCapAsync(dbPath, ct);
        var nbaK = await SweepNbaLineMoveAsync(dbPath, ct);
        await SweepSoccerAsync(dbPath, ct);

        Console.WriteLine("\n=== Recommended defaults (write into Guards.cs if changed) ===");
        Console.WriteLine($"edgeCap={nbaCap.Cap:P0} (negRemovedFolds={nbaCap.NegFolds}/{nbaCap.FoldsWithRemovals}, removed n={nbaCap.RemovedBets} pnl={nbaCap.RemovedPnl:F1})");
        Console.WriteLine($"nbaLineMoveK={nbaK.K} (negRemovedFolds={nbaK.NegFolds}/{nbaK.FoldsWithRemovals}, removed n={nbaK.RemovedBets} pnl={nbaK.RemovedPnl:F1})");
        Console.WriteLine($"soccerLineMoveK={SoccerLineMoveK} strikeMove={Guards.RecommendedStrikeMove}");
        return nbaCap;
    }

    public static async Task<CapChoice> SweepNbaEdgeCapAsync(string dbPath, CancellationToken ct = default)
    {
        Console.WriteLine("---- NBA edge-cap (PaceEffNormal, line-move off) ----");

        Console.WriteLine($"{"cap",6} {"folds",6} {"neg",5} {"remN",6} {"remPnl",8} {"remClv",8} {"keptN",6} {"keptPnl",8}");

        CapChoice? bestPass = null;
        CapChoice? bestAny = null;
        foreach (var cap in EdgeCaps)
        {
            var (neg, withRem, total, remN, remPnl, remClv, keptN, keptPnl) =
                await NbaAcrossFoldsAsync(dbPath, cap, lineMoveK: null, ct);
            Console.WriteLine(
                $"{cap,6:P0} {withRem,6}/{total,-3} {neg,5} {remN,6} {remPnl,8:F1} {remClv,8:F2} {keptN,6} {keptPnl,8:F1}");
            var choice = new CapChoice(cap, neg, withRem, total, remPnl, remN);
            if (bestAny is null || PreferCap(choice, bestAny.Value))
                bestAny = choice;
            if (Passes(neg, withRem) && (bestPass is null || PreferCap(choice, bestPass.Value)))
                bestPass = choice;
        }

        // Prefer a majority-neg proof; else least-bad ratio (still document FAIL).
        var pick = bestPass ?? bestAny ?? new CapChoice(Guards.RecommendedEdgeCap, 0, 0, 0, 0, 0);
        Console.WriteLine($"NBA edge-cap pick: {pick.Cap:P0} (majority-neg removed={(Passes(pick.NegFolds, pick.FoldsWithRemovals) ? "YES" : "NO")})\n");
        return pick;
    }

    public static async Task<KChoice> SweepNbaLineMoveAsync(string dbPath, CancellationToken ct = default)
    {
        Console.WriteLine("---- NBA line-move (PaceEffNormal, edge-cap off) ----");
        Console.WriteLine($"{"k",6} {"folds",6} {"neg",5} {"remN",6} {"remPnl",8} {"remClv",8}");

        KChoice? best = null;
        foreach (var k in NbaLineMoveKs)
        {
            var (neg, withRem, total, remN, remPnl, remClv, _, _) =
                await NbaAcrossFoldsAsync(dbPath, edgeCap: null, lineMoveK: k, ct);
            Console.WriteLine(
                $"{k,6:F1} {withRem,6}/{total,-3} {neg,5} {remN,6} {remPnl,8:F1} {remClv,8:F2}");
            var choice = new KChoice(k, neg, withRem, total, remPnl, remN);
            if (Passes(neg, withRem) && (best is null || PreferK(choice, best.Value)))
                best = choice;
        }

        var pick = best ?? new KChoice(Guards.RecommendedNbaLineMoveK, 0, 0, 0, 0, 0);
        Console.WriteLine($"NBA line-move pick: k={pick.K} (majority-neg removed={(Passes(pick.NegFolds, pick.FoldsWithRemovals) ? "YES" : "NO")})\n");
        return pick;
    }

    public static async Task SweepSoccerAsync(string dbPath, CancellationToken ct = default)
    {
        foreach (var sport in new[] { "SERIEA", "MLS", "LIGUE1", "LIGAMX" })
        {
            try { await SweepSoccerLeagueAsync(dbPath, sport, ct); }
            catch (Exception ex) { Console.WriteLine($"SKIP {sport}: {ex.Message}"); }
        }
    }

    static async Task SweepSoccerLeagueAsync(string dbPath, string sport, CancellationToken ct)
    {
        var (holdout, w, minEdge, minEv, alpha, useDc, dcAlpha) = SoccerChamp(sport);
        var folds = await PreHoldoutSeasonsAsync(dbPath, sport, holdout, ct);
        if (folds.Count == 0)
        {
            Console.WriteLine($"---- {sport} ---- no pre-holdout folds");
            return;
        }

        Console.WriteLine($"---- {sport} edge-cap (k off) + line-move k={SoccerLineMoveK} (cap off) ----");
        Console.WriteLine($"folds=[{string.Join(",", folds)}] holdout={holdout}");

        CapChoice? bestCap = null;
        Console.WriteLine($"{"mode",14} {"folds",6} {"neg",5} {"remN",6} {"remPnl",8} {"remClv",8}");
        foreach (var cap in EdgeCaps)
        {
            var (neg, withRem, total, remN, remPnl, remClv) = await SoccerAcrossFoldsAsync(
                dbPath, sport, folds, holdout, w, minEv, minEdge, alpha, useDc, dcAlpha,
                edgeCap: cap, lineMoveK: null, ct);
            Console.WriteLine($"cap={cap:P0}{"",4} {withRem,6}/{total,-3} {neg,5} {remN,6} {remPnl,8:F1} {remClv,8:F2}");
            var choice = new CapChoice(cap, neg, withRem, total, remPnl, remN);
            if (Passes(neg, withRem) && (bestCap is null || PreferCap(choice, bestCap.Value)))
                bestCap = choice;
        }

        var (negK, withRemK, totalK, remNK, remPnlK, remClvK) = await SoccerAcrossFoldsAsync(
            dbPath, sport, folds, holdout, w, minEv, minEdge, alpha, useDc, dcAlpha,
            edgeCap: null, lineMoveK: SoccerLineMoveK, ct);
        Console.WriteLine(
            $"k={SoccerLineMoveK:F1}{"",7} {withRemK,6}/{totalK,-3} {negK,5} {remNK,6} {remPnlK,8:F1} {remClvK,8:F2}");
        Console.WriteLine(
            $"{sport} pick cap={(bestCap?.Cap ?? Guards.RecommendedEdgeCap):P0} lineMoveProof={(Passes(negK, withRemK) ? "YES" : "NO")}\n");
    }

    /// <summary>NBA proof helper for tests: edge-cap removed-set majority negative.</summary>
    public static async Task<(int NegFolds, int FoldsWithRemovals, int TotalFolds, double RemovedPnl)>
        NbaEdgeCapProofAsync(string dbPath, double cap, CancellationToken ct = default)
    {
        var (neg, withRem, total, _, remPnl, _, _, _) =
            await NbaAcrossFoldsAsync(dbPath, cap, lineMoveK: null, ct);
        return (neg, withRem, total, remPnl);
    }

    /// <summary>NBA line-move proof helper for tests.</summary>
    public static async Task<(int NegFolds, int FoldsWithRemovals, int TotalFolds, double RemovedPnl)>
        NbaLineMoveProofAsync(string dbPath, double k, CancellationToken ct = default)
    {
        var (neg, withRem, total, _, remPnl, _, _, _) =
            await NbaAcrossFoldsAsync(dbPath, edgeCap: null, lineMoveK: k, ct);
        return (neg, withRem, total, remPnl);
    }

    static async Task<(int Neg, int WithRem, int Total, int RemN, double RemPnl, double RemClv, int KeptN, double KeptPnl)>
        NbaAcrossFoldsAsync(string dbPath, double? edgeCap, double? lineMoveK, CancellationToken ct)
    {
        const double w = ChampConfig.MarketWeight, minEv = ChampConfig.MinEv, edge = ChampConfig.MinAbsTotalEdge;
        var neg = 0;
        var withRem = 0;
        var remN = 0;
        var keptN = 0;
        double remPnl = 0, remClvSum = 0, keptPnl = 0;
        var total = NbaNormalSigmaTierA.FoldSeasons.Length;

        foreach (var season in NbaNormalSigmaTierA.FoldSeasons)
        {
            var (kept, removed) = await TotalsEdgeBacktest.RunSeasonFoldGuardedAsync(
                dbPath, season, w, minEv, edge, edgeCap, lineMoveK,
                TotalsModelKind.PaceEffNormal, ct: ct);
            keptN += kept.Bets;
            keptPnl += kept.PaperPnlUnits;
            if (removed.Bets == 0) continue;
            withRem++;
            remN += removed.Bets;
            remPnl += removed.PaperPnlUnits;
            remClvSum += removed.MeanClv * removed.Bets;
            if (removed.PaperPnlUnits < 0 || removed.MeanClv < 0)
                neg++;
        }

        var remClv = remN == 0 ? 0 : remClvSum / remN;
        return (neg, withRem, total, remN, remPnl, remClv, keptN, keptPnl);
    }

    static async Task<(int Neg, int WithRem, int Total, int RemN, double RemPnl, double RemClv)>
        SoccerAcrossFoldsAsync(
            string dbPath, string sport, List<string> folds, string holdout,
            double w, double minEv, double minEdge, double alpha, bool useDc, double dcAlpha,
            double? edgeCap, double? lineMoveK, CancellationToken ct)
    {
        var neg = 0;
        var withRem = 0;
        var remN = 0;
        double remPnl = 0, remClvSum = 0;

        foreach (var season in folds)
        {
            var (_, removed) = await SoccerLeagueTotalsBacktest.RunSeasonFoldGuardedAsync(
                dbPath, sport, season, holdout, w, minEv, minEdge, edgeCap, lineMoveK,
                useDixonColes: useDc, alpha: alpha, dcAlpha: dcAlpha, ct: ct);
            if (removed.Bets == 0) continue;
            withRem++;
            remN += removed.Bets;
            remPnl += removed.PaperPnlUnits;
            remClvSum += removed.MeanClv * removed.Bets;
            if (removed.PaperPnlUnits < 0 || removed.MeanClv < 0)
                neg++;
        }

        var remClv = remN == 0 ? 0 : remClvSum / remN;
        return (neg, withRem, folds.Count, remN, remPnl, remClv);
    }

    static bool Passes(int negFolds, int foldsWithRemovals)
        => foldsWithRemovals > 0 && negFolds * 2 > foldsWithRemovals;

    // Prefer more negative-removed folds, then more total removed PnL damage (more negative), then lower cap (stricter).
    static bool PreferCap(CapChoice a, CapChoice b)
    {
        if (a.NegFolds != b.NegFolds) return a.NegFolds > b.NegFolds;
        if (Math.Abs(a.RemovedPnl - b.RemovedPnl) > 1e-9) return a.RemovedPnl < b.RemovedPnl;
        return a.Cap < b.Cap;
    }

    static bool PreferK(KChoice a, KChoice b)
    {
        if (a.NegFolds != b.NegFolds) return a.NegFolds > b.NegFolds;
        if (Math.Abs(a.RemovedPnl - b.RemovedPnl) > 1e-9) return a.RemovedPnl < b.RemovedPnl;
        return a.K < b.K;
    }

    static (string Holdout, double W, double Edge, double Ev, double Alpha, bool Dc, double DcAlpha) SoccerChamp(string sport)
        => sport switch
        {
            "SERIEA" => ("2024-2025", SerieAChampConfig.MarketWeight, SerieAChampConfig.MinAbsTotalEdge,
                SerieAChampConfig.MinEv, SerieAChampConfig.DcAlpha, true, SerieAChampConfig.DcAlpha),
            "MLS" => ("2025", MlsChampConfig.MarketWeight, MlsChampConfig.MinAbsTotalEdge,
                MlsChampConfig.MinEv, MlsChampConfig.EmaAlpha, false, 0.12),
            "LIGUE1" => ("2024-2025", Ligue1ChampConfig.MarketWeight, Ligue1ChampConfig.MinAbsTotalEdge,
                Ligue1ChampConfig.MinEv, Ligue1ChampConfig.EmaAlpha, false, 0.12),
            "LIGAMX" => ("2024-2025", LigaMxChampConfig.MarketWeight, LigaMxChampConfig.MinAbsTotalEdge,
                LigaMxChampConfig.MinEv, LigaMxChampConfig.EmaAlpha, false, 0.12),
            _ => throw new ArgumentException($"No champ for {sport}"),
        };

    static async Task<List<string>> PreHoldoutSeasonsAsync(
        string dbPath, string sport, string holdout, CancellationToken ct)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={dbPath}").Options;
        await using var db = new AppDbContext(options);
        var seasons = await db.Games.AsNoTracking()
            .Where(g => g.Sport == sport && g.HomeScore != null && g.Season != null)
            .Select(g => g.Season!)
            .Distinct()
            .ToListAsync(ct);
        var withOdds = new List<string>();
        foreach (var s in seasons.Where(s => string.CompareOrdinal(s, holdout) < 0).OrderBy(s => s))
        {
            var gameIds = await db.Games.AsNoTracking()
                .Where(g => g.Sport == sport && g.Season == s)
                .Select(g => g.Id)
                .ToListAsync(ct);
            var n = await db.OddsSnapshots.AsNoTracking()
                .CountAsync(o => gameIds.Contains(o.GameId) && o.Market == "TOTAL" && o.PriceKind == PriceKind.Open, ct);
            if (n > 0) withOdds.Add(s);
        }
        return withOdds;
    }
}
