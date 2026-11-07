using Microsoft.EntityFrameworkCore;
using PossiblyMaybe.Data;
using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Cli.Backtest;

/// <summary>
/// Round 5 T4a: deferred Tier A fold tables for soccer B4 dists + B2 pooling.
/// One run each vs league V0 champ on the same open-line tickets; log every trial in
/// durable <see cref="TrialLedger"/> (<c>data/vault.db</c>). No paper promotion; no re-tune.
/// CLI: <c>tier-a-r5-soccer [SPORT|all] [db] [vault.db]</c>
/// </summary>
public static class SoccerR5TierA
{
    public static readonly string[] Sports = ["MLS", "LIGUE1", "LIGAMX", "SERIEA"];

    /// <summary>B4 ids scored once + B2 pooling.</summary>
    public static readonly string[] VariantIds =
        ["mls-dc", "+poisson-all", "+negbin", "+dc-rho", "+pooling"];

    public static async Task RunAsync(
        string dbPath,
        string vaultPath,
        string? sportFilter = null,
        CancellationToken ct = default)
    {
        Console.WriteLine("######## Round 5 T4a: soccer Tier A (B4 dists + B2 pooling) vs V0 ########");
        Console.WriteLine("Same open-line tickets as league V0 champ. AssumedJuice −110.");
        Console.WriteLine("Pass → Shadow only (no paper). Fail → shadow marked FAIL. No re-tune after numbers.");
        Console.WriteLine($"TrialLedger → {vaultPath}\n");

        var (_, trials) = VaultDb.Open(vaultPath);
        var sports = sportFilter is null
            ? Sports
            : new[] { sportFilter.ToUpperInvariant() };

        foreach (var sport in sports)
        {
            try { await RunLeagueAsync(dbPath, sport, trials, ct); }
            catch (Exception ex) { Console.WriteLine($"SKIP {sport}: {ex.Message}"); }
        }

        Console.WriteLine(trials.FormatReportFooter());
        Console.WriteLine($"TrialLedger N_trials={trials.NTrials} (durable @ {vaultPath})");
    }

    static async Task RunLeagueAsync(
        string dbPath, string sport, TrialLedger trials, CancellationToken ct)
    {
        var champ = Champ(sport);
        var folds = await PreHoldoutSeasonsAsync(dbPath, sport, champ.Holdout, ct);
        if (folds.Count == 0)
        {
            Console.WriteLine($"######## {sport} ######## no pre-holdout seasons with odds");
            return;
        }

        Console.WriteLine(
            $"######## {sport} ######## holdout(burned)={champ.Holdout} folds=[{string.Join(", ", folds)}]");
        Console.WriteLine(
            $"V0 champ w={champ.W} edge={champ.MinEdge} EV≥{champ.MinEv} {(champ.UseDc ? $"DC α={champ.DcAlpha}" : $"EMA α={champ.Alpha}")}");
        if (sport == "SERIEA")
            Console.WriteLine("V-SA0 context: Serie A stays shadow/radar; paper portfolio uses V-SA0 (league off).");

        foreach (var variantId in VariantIds)
        {
            if (variantId == "mls-dc" && sport != "MLS")
            {
                Console.WriteLine($"  {variantId,-12} N/A (MLS-only LeagueOff)");
                continue;
            }

            var summary = await ScoreVariantAsync(dbPath, sport, champ, folds, variantId, ct);
            var hash = Vault.StrategyHash($"t4a:{sport}:{variantId}", summary.ParamsLabel);
            trials.Record(hash, summary.ParamsLabel, folds[0], folds[^1], gridSize: 1);

            var pass = summary.Pass;
            var stage = pass ? "Shadow" : "Shadow-FAIL";
            Console.WriteLine(
                $"  {variantId,-12} folds={folds.Count} winFolds={summary.FoldWins}/{folds.Count} " +
                $"n={summary.NV0} clvV0={summary.MeanClvV0:F3} clvVar={summary.MeanClvVar:F3} " +
                $"pnlV0={summary.PnlV0:F1} pnlVar={summary.PnlVar:F1} " +
                $"llV0={summary.LogLossV0:F4} llVar={summary.LogLossVar:F4} " +
                $"brV0={summary.BrierV0:F4} brVar={summary.BrierVar:F4} " +
                $"kellyV0={summary.KellyV0:F1} kellyVar={summary.KellyVar:F1} " +
                $"phi={summary.MeanPhi:F3} VERDICT={(pass ? "PASS" : "FAIL")} → {stage}");
        }

        Console.WriteLine();
    }

    static async Task<VariantSummary> ScoreVariantAsync(
        string dbPath,
        string sport,
        ChampGates champ,
        IReadOnlyList<string> folds,
        string variantId,
        CancellationToken ct)
    {
        var foldWins = 0;
        var nTot = 0;
        double sumClvV0 = 0, sumClvVar = 0, sumPnlV0 = 0, sumPnlVar = 0;
        double sumLlV0 = 0, sumLlVar = 0, sumBrV0 = 0, sumBrVar = 0;
        double sumKellyV0 = 0, sumKellyVar = 0, sumPhi = 0;
        var phiFolds = 0;

        Console.WriteLine(
            $"  ---- {variantId} ----");
        Console.WriteLine(
            $"  {"fold",-12} {"n",5} {"clvV0",7} {"clvVar",7} {"pnlV0",7} {"pnlVar",7} " +
            $"{"llV0",7} {"llVar",7} {"kV0",8} {"kVar",8} {"dK",8} {"phi",6}");

        foreach (var fold in folds)
        {
            var foldResult = await RunFoldAsync(dbPath, sport, champ, fold, variantId, ct);
            if (foldResult.KellyVar > foldResult.KellyV0 + 1e-9)
                foldWins++;

            nTot += foldResult.N;
            sumClvV0 += foldResult.MeanClvV0 * foldResult.N;
            sumClvVar += foldResult.MeanClvVar * foldResult.N;
            sumPnlV0 += foldResult.PnlV0;
            sumPnlVar += foldResult.PnlVar;
            sumLlV0 += foldResult.LogLossV0 * foldResult.N;
            sumLlVar += foldResult.LogLossVar * foldResult.N;
            sumBrV0 += foldResult.BrierV0 * foldResult.N;
            sumBrVar += foldResult.BrierVar * foldResult.N;
            sumKellyV0 += foldResult.KellyV0;
            sumKellyVar += foldResult.KellyVar;
            if (foldResult.Phi is { } phi)
            {
                sumPhi += phi;
                phiFolds++;
            }

            Console.WriteLine(
                $"  {fold,-12} {foldResult.N,5} {foldResult.MeanClvV0,7:F3} {foldResult.MeanClvVar,7:F3} " +
                $"{foldResult.PnlV0,7:F1} {foldResult.PnlVar,7:F1} " +
                $"{foldResult.LogLossV0,7:F4} {foldResult.LogLossVar,7:F4} " +
                $"{foldResult.KellyV0,8:F1} {foldResult.KellyVar,8:F1} " +
                $"{foldResult.KellyVar - foldResult.KellyV0,8:F1} {foldResult.Phi ?? 0,6:F3}");
        }

        var meanClvV0 = nTot == 0 ? 0 : sumClvV0 / nTot;
        var meanClvVar = nTot == 0 ? 0 : sumClvVar / nTot;
        var llV0 = nTot == 0 ? 0 : sumLlV0 / nTot;
        var llVar = nTot == 0 ? 0 : sumLlVar / nTot;
        var brV0 = nTot == 0 ? 0 : sumBrV0 / nTot;
        var brVar = nTot == 0 ? 0 : sumBrVar / nTot;
        var meanPhi = phiFolds == 0 ? SoccerDistFamily.TrainPhi : sumPhi / phiFolds;

        // Pre-declared gate (B4 / PoissonCdfTierA): majority Kelly uplift, CLV ≥ V0, n≥40.
        // Identical-to-V0 (already-live curve) counts as PASS tie — no uplift possible.
        var majority = foldWins > folds.Count / 2;
        var clvOk = meanClvVar + 1e-12 >= meanClvV0;
        var nOk = nTot >= 40;
        var identical = nTot >= 40
            && Math.Abs(meanClvVar - meanClvV0) < 1e-9
            && Math.Abs(sumKellyVar - sumKellyV0) < 1e-6
            && Math.Abs(llVar - llV0) < 1e-9;
        var pass = nOk && clvOk && (majority || identical);

        return new VariantSummary(
            pass, foldWins, nTot, meanClvV0, meanClvVar, sumPnlV0, sumPnlVar,
            llV0, llVar, brV0, brVar, sumKellyV0, sumKellyVar, meanPhi,
            $"sport={sport};variant={variantId};folds={folds.Count};phi={meanPhi:F4};rho={SoccerDistFamily.TrainRho}");
    }

    /// <summary>
    /// Walk-forward one fold: fit φ on train only, then score V0 tickets on the hold season
    /// with both V0 and variant probabilities (same tickets protocol).
    /// </summary>
    public static async Task<FoldResult> RunFoldAsync(
        string dbPath,
        string sport,
        ChampGates champ,
        string fold,
        string variantId,
        CancellationToken ct = default)
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

        Dictionary<int, MlsGameXg> xgByGame = [];
        if (sport == "MLS")
        {
            try { xgByGame = await db.MlsGameXgs.AsNoTracking().ToDictionaryAsync(x => x.GameId, ct); }
            catch { /* older DB */ }
        }

        var goalsBook = new ScoringBook(alpha: champ.Alpha, leagueInit: 2.7);
        var xgBook = sport == "MLS" ? new ScoringBook(alpha: champ.Alpha, leagueInit: 2.7) : null;
        var seenXg = new HashSet<string>(StringComparer.Ordinal);
        var dcBook = champ.UseDc ? new DixonColesBook(alpha: champ.DcAlpha) : null;

        var trainPred = new List<double>();
        var trainAct = new List<double>();
        double? fittedPhi = null;

        var n = 0;
        double clvV0 = 0, clvVar = 0, pnlV0 = 0, pnlVar = 0;
        double llV0 = 0, llVar = 0, brV0 = 0, brVar = 0;
        var bankV0 = 1000.0;
        var bankVar = 1000.0;
        const int juice = -110;

        foreach (var g in games)
        {
            var season = g.Season ?? "";
            // MLS Tier C / post-fold: update only
            if (sport == "MLS" && season == "2026")
            {
                UpdateBooks(g, xgByGame, xgBook, goalsBook, seenXg, dcBook);
                continue;
            }
            if (sport != "MLS" && string.CompareOrdinal(season, fold) > 0)
            {
                goalsBook.Update(g.HomeTeam, g.AwayTeam, g.HomeScore!.Value, g.AwayScore!.Value);
                dcBook?.Update(g.HomeTeam, g.AwayTeam, g.HomeScore!.Value, g.AwayScore!.Value);
                continue;
            }

            byOdds.TryGetValue(g.Id, out var snaps);
            snaps ??= [];
            var open = snaps.FirstOrDefault(s => s.PriceKind == PriceKind.Open);
            var close = snaps.FirstOrDefault(s => s.PriceKind == PriceKind.Close);

            var (lh0, la0) = Lambdas(g, sport, xgByGame, xgBook, goalsBook, seenXg, dcBook);
            var modelExp0 = lh0 + la0;

            if (open?.Line is not { } line || close?.Line is not { } closeLine
                || Math.Abs(line - closeLine) < 0.25)
            {
                if (string.CompareOrdinal(season, fold) < 0)
                {
                    trainPred.Add(modelExp0);
                    trainAct.Add(g.HomeScore!.Value + g.AwayScore!.Value);
                }
                UpdateBooks(g, xgByGame, xgBook, goalsBook, seenXg, dcBook);
                continue;
            }

            if (OregonExclusion.IsExcluded(sport, g.HomeTeam, g.AwayTeam))
            {
                UpdateBooks(g, xgByGame, xgBook, goalsBook, seenXg, dcBook);
                continue;
            }

            // Train: collect μ for φ; do not score hold yet.
            if (string.CompareOrdinal(season, fold) < 0)
            {
                trainPred.Add(modelExp0);
                trainAct.Add(g.HomeScore!.Value + g.AwayScore!.Value);
                UpdateBooks(g, xgByGame, xgBook, goalsBook, seenXg, dcBook);
                continue;
            }

            if (season != fold)
            {
                UpdateBooks(g, xgByGame, xgBook, goalsBook, seenXg, dcBook);
                continue;
            }

            // First hold game: lock φ from train only (negbin).
            fittedPhi ??= variantId == "+negbin"
                ? TotalsMath.FitNegBinPhi(trainPred, trainAct)
                : SoccerDistFamily.TrainPhi;
            // Cap absurd MoM φ; fall back to pre-registered train prior when train empty/degenerate.
            if (fittedPhi is < 1e-9 or > 2.0)
                fittedPhi = SoccerDistFamily.TrainPhi;

            var phase = SeasonPhaseFor(sport, g.TipoffUtc);
            var poolingSubset = variantId == "+pooling";
            if (poolingSubset && !IsPoolingPhase(phase))
            {
                UpdateBooks(g, xgByGame, xgBook, goalsBook, seenXg, dcBook);
                continue;
            }

            var (lh, la, modelExp) = ApplyPoolingIfNeeded(variantId, phase, lh0, la0, modelExp0);
            var expV0 = champ.W * line + (1 - champ.W) * modelExp0;
            var edgeV0 = expV0 - line;
            var expVar = champ.W * line + (1 - champ.W) * modelExp;
            // Same tickets = V0 edge gate on unpooled means (pooling only changes P / Kelly on those tickets).
            // For +pooling, B2 soft-spot protocol: gate on pooled edge within the tagged subset.
            var edgeGate = poolingSubset ? (expVar - line) : edgeV0;
            if (Math.Abs(edgeGate) < champ.MinEdge)
            {
                UpdateBooks(g, xgByGame, xgBook, goalsBook, seenXg, dcBook);
                continue;
            }

            var pOverV0 = ChampOverProb(sport, lh0, la0, expV0, line);
            var pOverVar = VariantOverProb(variantId, sport, lh, la, expVar, line, fittedPhi.Value);

            var betOver = edgeGate > 0;
            var pSideV0 = betOver ? pOverV0 : 1 - pOverV0;
            var pSideVar = betOver ? pOverVar : 1 - pOverVar;
            // V0 ticket EV gate (same tickets). Pooling uses its own p for the gate on the subset.
            var pGate = poolingSubset ? pSideVar : pSideV0;
            if (OddsMath.ExpectedValue(pGate, juice) < champ.MinEv)
            {
                UpdateBooks(g, xgByGame, xgBook, goalsBook, seenXg, dcBook);
                continue;
            }

            var ou = TotalsMath.OuResult(g.HomeScore!.Value, g.AwayScore!.Value, line);
            var clv = betOver
                ? TotalsMath.OverClvPoints(line, closeLine)
                : -TotalsMath.OverClvPoints(line, closeLine);
            var won = betOver ? ou > 0 : ou < 0;
            if (ou == 0)
            {
                UpdateBooks(g, xgByGame, xgBook, goalsBook, seenXg, dcBook);
                continue;
            }

            var flat = TotalsMath.OuPnL(juice, betOver ? ou : -ou);
            n++;
            clvV0 += clv;
            clvVar += clv; // same side / same ticket → same CLV
            pnlV0 += flat;
            pnlVar += flat;
            AccumulateLlBr(pSideV0, won, ref llV0, ref brV0);
            AccumulateLlBr(pSideVar, won, ref llVar, ref brVar);

            var stakeV0 = bankV0 * Math.Min(0.02, OddsMath.FractionalKelly(pSideV0, juice, 0.25));
            if (stakeV0 >= 0.50) bankV0 += stakeV0 * flat;
            var stakeVar = bankVar * Math.Min(0.02, OddsMath.FractionalKelly(pSideVar, juice, 0.25));
            if (stakeVar >= 0.50) bankVar += stakeVar * flat;

            UpdateBooks(g, xgByGame, xgBook, goalsBook, seenXg, dcBook);
        }

        return new FoldResult(
            n,
            n == 0 ? 0 : clvV0 / n,
            n == 0 ? 0 : clvVar / n,
            pnlV0,
            pnlVar,
            n == 0 ? 0 : llV0 / n,
            n == 0 ? 0 : llVar / n,
            n == 0 ? 0 : brV0 / n,
            n == 0 ? 0 : brVar / n,
            bankV0 - 1000.0,
            bankVar - 1000.0,
            fittedPhi);
    }

    static void AccumulateLlBr(double pSide, bool won, ref double ll, ref double br)
    {
        var p = Math.Clamp(pSide, 1e-6, 1 - 1e-6);
        var y = won ? 1.0 : 0.0;
        ll += -(y * Math.Log(p) + (1 - y) * Math.Log(1 - p));
        br += (p - y) * (p - y);
    }

    static double ChampOverProb(string sport, double lh, double la, double exp, double line)
        => SoccerOverProbConfig.OverProb(lh, la, exp, line, sport);

    static double VariantOverProb(
        string variantId, string sport, double lh, double la, double exp, double line, double phi)
        => variantId switch
        {
            "mls-dc" or "+poisson-all" => TotalsMath.PoissonTotalOverProb(lh, la, line),
            "+negbin" => TotalsMath.NegBinTotalOverProb(lh + la, phi, line),
            "+dc-rho" => DixonColesBook.OverProbWithRho(lh, la, line, SoccerDistFamily.TrainRho),
            "+pooling" => ChampOverProb(sport, lh, la, exp, line),
            _ => ChampOverProb(sport, lh, la, exp, line),
        };

    static (double Lh, double La, double Exp) ApplyPoolingIfNeeded(
        string variantId, string? phase, double lh, double la, double modelExp)
    {
        if (variantId != "+pooling" || !IsPoolingPhase(phase))
            return (lh, la, modelExp);
        var mean = PoolingFamily.LeagueMeanTotal("MLS"); // soccer mean 2.70
        var w = PoolingFamily.EarlySeasonWeight;
        var exp = PoolingFamily.Shrink(modelExp, mean, w);
        lh = PoolingFamily.Shrink(lh, mean / 2.0, w);
        la = PoolingFamily.Shrink(la, mean / 2.0, w);
        return (lh, la, exp);
    }

    /// <summary>A5 opener tags that trigger +pooling (incl. Liga MX apertura/clausura openers).</summary>
    public static bool IsPoolingPhase(string? phase)
    {
        if (phase is null) return false;
        if (PoolingFamily.PoolingPhases.Contains(phase)) return true;
        // A5 Liga MX calendars use apertura-opening / clausura-opening (pre-declared openers).
        return phase.Equals("apertura-opening", StringComparison.OrdinalIgnoreCase)
            || phase.Equals("clausura-opening", StringComparison.OrdinalIgnoreCase);
    }

    public static string? SeasonPhaseFor(string sport, DateTimeOffset tipoff)
    {
        var yyyymmdd = tipoff.UtcDateTime.ToString("yyyyMMdd");
        return SeasonalityPhases.Resolve(sport, yyyymmdd);
    }

    static (double Lh, double La) Lambdas(
        Game g, string sport,
        Dictionary<int, MlsGameXg> xgByGame,
        ScoringBook? xgBook,
        ScoringBook goalsBook,
        HashSet<string> seenXg,
        DixonColesBook? dcBook)
    {
        if (sport == "MLS" && xgBook is not null)
        {
            var useXg = seenXg.Contains(g.HomeTeam) && seenXg.Contains(g.AwayTeam);
            return useXg
                ? xgBook.ExpectedLambdas(g.HomeTeam, g.AwayTeam)
                : goalsBook.ExpectedLambdas(g.HomeTeam, g.AwayTeam);
        }
        return dcBook?.ExpectedLambdas(g.HomeTeam, g.AwayTeam)
            ?? goalsBook.ExpectedLambdas(g.HomeTeam, g.AwayTeam);
    }

    static void UpdateBooks(
        Game g,
        Dictionary<int, MlsGameXg> xgByGame,
        ScoringBook? xgBook,
        ScoringBook goalsBook,
        HashSet<string> seenXg,
        DixonColesBook? dcBook)
    {
        goalsBook.Update(g.HomeTeam, g.AwayTeam, g.HomeScore!.Value, g.AwayScore!.Value);
        dcBook?.Update(g.HomeTeam, g.AwayTeam, g.HomeScore!.Value, g.AwayScore!.Value);
        if (xgBook is null) return;
        if (!xgByGame.TryGetValue(g.Id, out var x)) return;
        xgBook.Update(g.HomeTeam, g.AwayTeam, x.HomeXg, x.AwayXg);
        seenXg.Add(g.HomeTeam);
        seenXg.Add(g.AwayTeam);
    }

    public static ChampGates Champ(string sport) => sport switch
    {
        "SERIEA" => new("2024-2025", SerieAChampConfig.MarketWeight, SerieAChampConfig.MinAbsTotalEdge,
            SerieAChampConfig.MinEv, SerieAChampConfig.DcAlpha, true, SerieAChampConfig.DcAlpha),
        "MLS" => new("2025", MlsChampConfig.MarketWeight, MlsChampConfig.MinAbsTotalEdge,
            MlsChampConfig.MinEv, MlsChampConfig.EmaAlpha, false, 0.12),
        "LIGUE1" => new("2024-2025", Ligue1ChampConfig.MarketWeight, Ligue1ChampConfig.MinAbsTotalEdge,
            Ligue1ChampConfig.MinEv, Ligue1ChampConfig.EmaAlpha, false, 0.12),
        "LIGAMX" => new("2024-2025", LigaMxChampConfig.MarketWeight, LigaMxChampConfig.MinAbsTotalEdge,
            LigaMxChampConfig.MinEv, LigaMxChampConfig.EmaAlpha, false, 0.12),
        _ => throw new ArgumentException($"No champ for {sport}"),
    };

    public static async Task<List<string>> PreHoldoutSeasonsAsync(
        string dbPath, string sport, string holdout, CancellationToken ct = default)
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
                .CountAsync(o => o.Market == "TOTAL" && gameIds.Contains(o.GameId), ct);
            if (n >= 20) withOdds.Add(s);
        }
        return withOdds;
    }

    public readonly record struct ChampGates(
        string Holdout, double W, double MinEdge, double MinEv, double Alpha, bool UseDc, double DcAlpha);

    public readonly record struct FoldResult(
        int N,
        double MeanClvV0, double MeanClvVar,
        double PnlV0, double PnlVar,
        double LogLossV0, double LogLossVar,
        double BrierV0, double BrierVar,
        double KellyV0, double KellyVar,
        double? Phi);

    readonly record struct VariantSummary(
        bool Pass, int FoldWins, int NV0,
        double MeanClvV0, double MeanClvVar,
        double PnlV0, double PnlVar,
        double LogLossV0, double LogLossVar,
        double BrierV0, double BrierVar,
        double KellyV0, double KellyVar,
        double MeanPhi, string ParamsLabel);
}
