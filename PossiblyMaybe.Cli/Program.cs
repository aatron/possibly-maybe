using Microsoft.EntityFrameworkCore;
using PossiblyMaybe.Cli.Backtest;
using PossiblyMaybe.Cli.Import;
using PossiblyMaybe.Cli.Paper;
using PossiblyMaybe.Cli.Variants;
using PossiblyMaybe.Data;
using PossiblyMaybe.Domain;

if (args.Length == 0)
{
    PrintUsage();
    return 0;
}

var command = args[0].ToLowerInvariant();
switch (command)
{
    case "import":
    case "import-all":
    {
        var dbPath = "possiblymaybe.db";
        if (args.Length > 1 && !args[1].Equals("all", StringComparison.OrdinalIgnoreCase))
            dbPath = args[1];
        else if (args.Length > 2)
            dbPath = args[2];

        var rawRoot = ResolveRawRoot();
        Console.WriteLine($"Raw root: {rawRoot}");
        Console.WriteLine($"Importing into {Path.GetFullPath(dbPath)} …");
        var qc = await Gate0Importer.RunAsync(rawRoot, dbPath);
        Console.WriteLine("--- QC ---");
        Console.WriteLine($"Games:              {qc.Games}");
        Console.WriteLine($"Odds total:         {qc.Odds}");
        Console.WriteLine($"Odds OpenClose:     {qc.OddsOpenClose}");
        Console.WriteLine($"Odds CloseOnly:     {qc.OddsCloseOnly}");
        Console.WriteLine($"Odds AssumedJuice:  {qc.OddsAssumedJuice}");
        Console.WriteLine($"Refs:               {qc.Refs}");
        Console.WriteLine($"Donaghy-flagged:    {qc.DonaghyFlagged}");
        Console.WriteLine($"ESPN ids attached:  {qc.EspnIdsAttached}");
        var mlPct = qc.Holdout2024Games == 0 ? 0 : 100.0 * qc.Holdout2024WithMlClose / qc.Holdout2024Games;
        var refPct = qc.Holdout2024Games == 0 ? 0 : 100.0 * qc.Holdout2024WithRefs / qc.Holdout2024Games;
        Console.WriteLine(
            $"Holdout 2023-24:    {qc.Holdout2024Games} games; close-ML {qc.Holdout2024WithMlClose} ({mlPct:F1}%); refs {qc.Holdout2024WithRefs} ({refPct:F1}%)");
        Console.WriteLine("Note: OpenClose ML from ESPN API (2024+) and E fair probs (2024-25).");
        return 0;
    }
    case "backtest":
    {
        var repoRoot = FindRepoRoot();
        var dbPath = args.Length > 1 && !args[1].StartsWith('-')
            ? args[1]
            : Path.Combine(repoRoot, "possiblymaybe.db");
        if (!File.Exists(dbPath))
        {
            Console.Error.WriteLine($"DB not found: {dbPath}. Run import first.");
            return 2;
        }

        Console.WriteLine("NOTE: ML+spread open→close from ESPN/B; combo picks best EV per game.");
        foreach (var mode in new[] { BetMarketMode.Moneyline, BetMarketMode.Spread, BetMarketMode.ComboBest })
        {
            var (train, holdout) = await Gate1ComboBacktest.RunAsync(dbPath, mode);
            Console.WriteLine($"===== market={mode} model=MarketEloRefsLoad =====");
            PrintSlice(train);
            PrintSlice(holdout);
        }
        return 0;
    }
    case "sweep":
    {
        var repoRoot = FindRepoRoot();
        var dbPath = args.Length > 1 ? args[1] : Path.Combine(repoRoot, "possiblymaybe.db");
        if (!File.Exists(dbPath))
        {
            Console.Error.WriteLine($"DB not found: {dbPath}. Run import first.");
            return 2;
        }
        await SpreadSweep.RunAsync(dbPath);
        return 0;
    }
    case "import-totals":
    {
        var repoRoot = FindRepoRoot();
        var dbPath = args.Length > 1 ? args[1] : Path.Combine(repoRoot, "possiblymaybe.db");
        await Gate0Importer.ImportTotalsOnlyAsync(ResolveRawRoot(), dbPath);
        return 0;
    }
    case "import-box":
    {
        var repoRoot = FindRepoRoot();
        var dbPath = args.Length > 1 ? args[1] : Path.Combine(repoRoot, "possiblymaybe.db");
        await Gate0Importer.ImportBoxOnlyAsync(ResolveRawRoot(), dbPath);
        return 0;
    }
    case "refresh-results":
    {
        // refresh-results [yyyymmdd through, default yesterday UTC] [db] — NBA/MLS/LIGUE1/LIGAMX/SERIEA finals from ESPN (+NBA box, MLS xG)
        var repoRoot = FindRepoRoot();
        var through = args.Length > 1 && args[1].Length == 8
            ? DateOnly.ParseExact(args[1], "yyyyMMdd")
            : DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);
        var dbPath = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "possiblymaybe.db");
        await ResultsRefresh.RunAsync(dbPath, through);
        return 0;
    }
    case "import-mlb":
    {
        var repoRoot = FindRepoRoot();
        var dbPath = args.Length > 1 ? args[1] : Path.Combine(repoRoot, "possiblymaybe.db");
        await MultiSportImporter.ImportMlbAsync(ResolveRawRoot(), dbPath);
        return 0;
    }
    case "import-nfl":
    {
        var repoRoot = FindRepoRoot();
        var dbPath = args.Length > 1 ? args[1] : Path.Combine(repoRoot, "possiblymaybe.db");
        await MultiSportImporter.ImportNflAsync(ResolveRawRoot(), dbPath);
        return 0;
    }
    case "import-mls":
    {
        var repoRoot = FindRepoRoot();
        var dbPath = args.Length > 1 ? args[1] : Path.Combine(repoRoot, "possiblymaybe.db");
        await MultiSportImporter.ImportMlsAsync(ResolveRawRoot(), dbPath);
        return 0;
    }
    case "import-round2-odds":
    {
        // D1 flancast O→C: NFL overlay + MLB 2011–2021 append (no wipe)
        var repoRoot = FindRepoRoot();
        var dbPath = args.Length > 1 ? args[1] : Path.Combine(repoRoot, "possiblymaybe.db");
        await Round2OddsImporter.ImportFlancastAsync(ResolveRawRoot(), dbPath);
        return 0;
    }
    case "import-line-tracker":
    {
        // line_snapshots.db → OddsSnapshots (open/close); never touches paper.db
        var repoRoot = FindRepoRoot();
        var snapDb = args.Length > 1 ? args[1] : Path.Combine(repoRoot, "data", "line_snapshots.db");
        var dbPath = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "possiblymaybe.db");
        await LineTrackerImporter.ImportAsync(snapDb, dbPath);
        return 0;
    }
    case "import-disk-odds":
    {
        // W15: SBRO NFL/NBA/NCAA + Arnav MLB already on disk; prints before→after O→C coverage
        var repoRoot = FindRepoRoot();
        var dbPath = args.Length > 1 ? args[1] : Path.Combine(repoRoot, "possiblymaybe.db");
        await DiskOddsImporter.ImportAsync(ResolveRawRoot(), dbPath);
        return 0;
    }
    case "import-soccer":
    {
        // import-soccer <SPORT> <folder> [db]  e.g. import-soccer EFL championship
        if (args.Length < 3)
        {
            Console.Error.WriteLine("usage: import-soccer <SPORT> <folder> [db]  (folder under data/raw/soccer/)");
            return 1;
        }
        var repoRoot = FindRepoRoot();
        var dbPath = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "possiblymaybe.db");
        await MultiSportImporter.ImportSoccerLeagueAsync(ResolveRawRoot(), dbPath, args[1].ToUpperInvariant(), args[2]);
        return 0;
    }
    case "sim-stakes":
    {
        var repoRoot = FindRepoRoot();
        var dbPath = args.Length > 1 ? args[1] : Path.Combine(repoRoot, "possiblymaybe.db");
        if (!File.Exists(dbPath))
        {
            Console.Error.WriteLine($"DB not found: {dbPath}");
            return 2;
        }
        await StakeLaneHoldoutSim.RunAsync(dbPath);
        return 0;
    }
    case "sweep-totals":
    {
        var repoRoot = FindRepoRoot();
        var dbPath = args.Length > 1 && !args[1].StartsWith('-')
            ? args[1]
            : Path.Combine(repoRoot, "possiblymaybe.db");
        if (!File.Exists(dbPath))
        {
            Console.Error.WriteLine($"DB not found: {dbPath}. Run import then import-totals.");
            return 2;
        }
        // `sweep-totals [db] --cal` → same-gate Platt/Ridge vs PaceEff only
        // `sweep-totals [db] --absence` → same-gate PaceEff vs tip-time DNP absence adj
        // `sweep-totals [db] --normal-sigma` → Tier A Normal residual-σ vs logistic champ
        if (args.Any(a => a is "--cal" or "cal"))
            await TotalsSweep.CompareChampGatesAsync(dbPath);
        else if (args.Any(a => a is "--absence" or "absence"))
        {
            var scale = 0.5;
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (args[i] is "--scale" or "scale"
                    && double.TryParse(args[i + 1], out var s))
                    scale = s;
            }

            await TotalsSweep.CompareAbsenceAsync(dbPath, scale);
        }
        else if (args.Any(a => a is "--normal-sigma" or "normal-sigma"))
            await NbaNormalSigmaTierA.RunAsync(dbPath);
        else
            await TotalsSweep.RunAsync(dbPath);
        return 0;
    }
    case "sweep-mlb":
    {
        var repoRoot = FindRepoRoot();
        var dbPath = args.Length > 1 ? args[1] : Path.Combine(repoRoot, "possiblymaybe.db");
        var pf = Path.Combine(ResolveRawRoot(), "mlb", "park_factors.csv");
        await MlbTotalsBacktest.SweepAsync(dbPath, pf);
        return 0;
    }
    case "sweep-nfl":
    {
        var repoRoot = FindRepoRoot();
        var dbPath = args.Length > 1 ? args[1] : Path.Combine(repoRoot, "possiblymaybe.db");
        await NflTotalsBacktest.SweepAsync(dbPath);
        return 0;
    }
    case "sweep-mls":
    {
        var repoRoot = FindRepoRoot();
        var dbPath = args.Length > 1 ? args[1] : Path.Combine(repoRoot, "possiblymaybe.db");
        await MlsTotalsBacktest.SweepAsync(dbPath);
        return 0;
    }
    case "sweep-soccer":
    {
        // sweep-soccer <SPORT> <holdoutSeason> [db]  e.g. sweep-soccer EFL 2024-2025
        if (args.Length < 3)
        {
            Console.Error.WriteLine("usage: sweep-soccer <SPORT> <holdoutSeason> [db]");
            return 1;
        }
        var repoRoot = FindRepoRoot();
        var dbPath = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "possiblymaybe.db");
        await SoccerLeagueTotalsBacktest.SweepAsync(dbPath, args[1].ToUpperInvariant(), args[2]);
        return 0;
    }
    case "sweep-soccer-dc":
    {
        // sweep-soccer-dc <SPORT> <holdoutSeason> [db]  — Dixon–Coles att-def EMA
        if (args.Length < 3)
        {
            Console.Error.WriteLine("usage: sweep-soccer-dc <SPORT> <holdoutSeason> [db]");
            return 1;
        }
        var repoRoot = FindRepoRoot();
        var sport = args[1].ToUpperInvariant();
        var dbPath = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "possiblymaybe.db");
        if (sport == "MLS")
            await MlsTotalsBacktest.SweepDixonColesAsync(dbPath);
        else
            await SoccerLeagueTotalsBacktest.SweepDixonColesAsync(dbPath, sport, args[2]);
        return 0;
    }
    case "tier-a-poisson":
    {
        // tier-a-poisson [SPORT|all] [db] — Tier A walk-forward poisson-cdf-pover (no Tier C)
        var repoRoot = FindRepoRoot();
        var filter = args.Length > 1 && !args[1].EndsWith(".db", StringComparison.OrdinalIgnoreCase)
            && !args[1].Equals("all", StringComparison.OrdinalIgnoreCase)
            ? args[1]
            : null;
        var dbPath = args.Length > 1 && args[^1].EndsWith(".db", StringComparison.OrdinalIgnoreCase)
            ? args[^1]
            : Path.Combine(repoRoot, "possiblymaybe.db");
        if (!File.Exists(dbPath))
        {
            // worktree often has no local db — try main checkout
            var alt = Path.GetFullPath(Path.Combine(repoRoot, "..", "possibly-maybe", "possiblymaybe.db"));
            if (File.Exists(alt)) dbPath = alt;
        }
        if (!File.Exists(dbPath))
        {
            Console.Error.WriteLine($"DB not found: {dbPath}");
            return 2;
        }
        await PoissonCdfTierA.RunAsync(dbPath, filter);
        return 0;
    }
    case "tier-a-guards":
    {
        // tier-a-guards [db] — edge-cap + line-move Tier A sweep + removed-bet proof
        var repoRoot = FindRepoRoot();
        var dbPath = args.Length > 1 && args[^1].EndsWith(".db", StringComparison.OrdinalIgnoreCase)
            ? args[^1]
            : Path.Combine(repoRoot, "possiblymaybe.db");
        if (!File.Exists(dbPath))
        {
            var alt = Path.GetFullPath(Path.Combine(repoRoot, "..", "possibly-maybe", "possiblymaybe.db"));
            if (File.Exists(alt)) dbPath = alt;
        }
        if (!File.Exists(dbPath))
        {
            Console.Error.WriteLine($"DB not found: {dbPath}");
            return 2;
        }
        await GuardSweepTierA.RunAsync(dbPath);
        return 0;
    }
    case "tier-a-nfl-wind":
    {
        // tier-a-nfl-wind [db] — nfl-wind alone vs EPA+roof on filled O→C folds
        var repoRoot = FindRepoRoot();
        var dbPath = args.Length > 1 && args[^1].EndsWith(".db", StringComparison.OrdinalIgnoreCase)
            ? args[^1]
            : Path.Combine(repoRoot, "possiblymaybe.db");
        if (!File.Exists(dbPath))
        {
            var alt = Path.GetFullPath(Path.Combine(repoRoot, "..", "possibly-maybe", "possiblymaybe.db"));
            if (File.Exists(alt)) dbPath = alt;
        }
        if (!File.Exists(dbPath))
        {
            Console.Error.WriteLine($"DB not found: {dbPath}");
            return 2;
        }
        await NflWindTierA.RunAsync(dbPath);
        return 0;
    }
    case "tier-a-shutoff":
    {
        // tier-a-shutoff [db] — V3 vs V4 rolling-CLV pause: flat PnL + max drawdown
        var repoRoot = FindRepoRoot();
        var dbPath = args.Length > 1 && args[^1].EndsWith(".db", StringComparison.OrdinalIgnoreCase)
            ? args[^1]
            : Path.Combine(repoRoot, "possiblymaybe.db");
        if (!File.Exists(dbPath))
        {
            var alt = Path.GetFullPath(Path.Combine(repoRoot, "..", "possibly-maybe", "possiblymaybe.db"));
            if (File.Exists(alt)) dbPath = alt;
        }
        if (!File.Exists(dbPath))
        {
            Console.Error.WriteLine($"DB not found: {dbPath}");
            return 2;
        }
        var rows = await ShutoffProof.RunAsync(dbPath);
        ShutoffProof.Print(rows);
        return 0;
    }
    case "tier-c-poisson":
    {
        // Parent look-once only. Do not re-run after results are logged.
        var repoRoot = FindRepoRoot();
        var dbPath = args.Length > 1 && args[^1].EndsWith(".db", StringComparison.OrdinalIgnoreCase)
            ? args[^1]
            : Path.Combine(repoRoot, "possiblymaybe.db");
        if (!File.Exists(dbPath))
        {
            var alt = Path.GetFullPath(Path.Combine(repoRoot, "..", "possibly-maybe", "possiblymaybe.db"));
            if (File.Exists(alt)) dbPath = alt;
        }
        if (!File.Exists(dbPath))
        {
            Console.Error.WriteLine($"DB not found: {dbPath}");
            return 2;
        }
        await PoissonCdfTierC.RunAsync(dbPath);
        return 0;
    }
    case "sweep-decay":
    {
        // Library: EMA half-life / α + sparse B2B gate (TRAIN select; holdout once)
        var repoRoot = FindRepoRoot();
        var dbPath = args.Length > 1 ? args[1] : Path.Combine(repoRoot, "possiblymaybe.db");
        if (!File.Exists(dbPath))
        {
            Console.Error.WriteLine($"DB not found: {dbPath}");
            return 2;
        }
        await TimeDecaySweep.RunAsync(dbPath);
        return 0;
    }
    case "advise":
    case "advise-ou":
    {
        // Primary champ: advise-ou <modelExpectedTotal> <openTotal> [juice=-110] [bankroll]
        if (args.Length < 3
            || !double.TryParse(args[1], out var modelExp)
            || !double.TryParse(args[2], out var openTotal))
        {
            Console.Error.WriteLine("usage: advise-ou <modelExpectedTotal> <openTotal> [juice=-110] [bankroll]");
            Console.Error.WriteLine($"champ TOTAL: w={ChampConfig.MarketWeight} |exp-line|>={ChampConfig.MinAbsTotalEdge} EV>={ChampConfig.MinEv:P0}");
            return 1;
        }
        var juice = args.Length > 3 && int.TryParse(args[3], out var j) ? j : -110;
        var bankroll = args.Length > 4 && double.TryParse(args[4], out var b) ? b : ChampConfig.BankrollSeed;
        var exp = ChampConfig.MarketWeight * openTotal + (1 - ChampConfig.MarketWeight) * modelExp;
        var edge = exp - openTotal;
        if (Math.Abs(edge) < ChampConfig.MinAbsTotalEdge)
        {
            Console.WriteLine("NO-GO");
            Console.WriteLine($"edge={edge:F2} pts < min {ChampConfig.MinAbsTotalEdge}");
            return 3;
        }
        var betOver = edge > 0;
        // NBA path: Normal CDF with locked expanding-train fallback σ (same as slate/backtest).
        var pOver = TotalsMath.NormalOverProb(exp, openTotal, ExpandingResidualSigma.FallbackSigma);
        var p = betOver ? pOver : 1 - pOver;
        var advice = BetDecider.Advise(p, juice, bankroll, ChampConfig.MinEv);
        Console.WriteLine(advice.Go ? "GO" : "NO-GO");
        Console.WriteLine($"side={(betOver ? "OVER" : "UNDER")} total={openTotal:F1} edge={edge:F2} EV={advice.Ev:P2}");
        Console.WriteLine($"stakeFrac={advice.StakeFraction:P2}  {advice.Reason}");
        return advice.Go ? 0 : 3;
    }
    case "slate":
    case "slate-nba":
    {
        var repoRoot = FindRepoRoot();
        var dbPath = Path.Combine(repoRoot, "possiblymaybe.db");
        var date = args.Length > 1 && args[1].Length == 8 ? args[1]
            : DateTime.UtcNow.ToString("yyyyMMdd");
        var bank = args.Length > 2 && double.TryParse(args[^1], out var b) ? b : ChampConfig.BankrollSeed;
        // allow: slate-nba [date] [db] [bank] — keep it simple: slate-nba [yyyymmdd] [bank]
        if (args.Length > 1 && args[1].EndsWith(".db", StringComparison.OrdinalIgnoreCase))
            dbPath = args[1];
        if (!File.Exists(dbPath))
        {
            Console.Error.WriteLine($"DB not found: {dbPath}");
            return 2;
        }
        await DailySlate.RunNbaAsync(dbPath, date, bank);
        return 0;
    }
    case "slate-mls":
    {
        var repoRoot = FindRepoRoot();
        var dbPath = Path.Combine(repoRoot, "possiblymaybe.db");
        var date = args.Length > 1 && args[1].Length == 8 ? args[1]
            : DateTime.UtcNow.ToString("yyyyMMdd");
        var bank = args.Length > 2 && double.TryParse(args[2], out var b) ? b : MlsChampConfig.BankrollSeed;
        if (!File.Exists(dbPath))
        {
            Console.Error.WriteLine($"DB not found: {dbPath}");
            return 2;
        }
        await DailySlate.RunMlsAsync(dbPath, date, bank);
        return 0;
    }
    case "slate-soccer":
    {
        // slate-soccer <SPORT> [yyyymmdd] [bank]  — MLS|LIGUE1|LIGAMX|SERIEA
        if (args.Length < 2)
        {
            Console.Error.WriteLine("usage: slate-soccer <MLS|LIGUE1|LIGAMX|SERIEA> [yyyymmdd] [bank]");
            return 1;
        }
        var repoRoot = FindRepoRoot();
        var dbPath = Path.Combine(repoRoot, "possiblymaybe.db");
        var date = args.Length > 2 && args[2].Length == 8 ? args[2]
            : DateTime.UtcNow.ToString("yyyyMMdd");
        var bank = args.Length > 3 && double.TryParse(args[3], out var b) ? b : 1000.0;
        if (!File.Exists(dbPath))
        {
            Console.Error.WriteLine($"DB not found: {dbPath}");
            return 2;
        }
        await DailySlate.RunSoccerAsync(args[1], dbPath, date, bank);
        return 0;
    }
    case "advise-mls":
    {
        // Paper MLS champ: advise-mls <modelExpectedGoals> <openTotal> [juice=-110] [bankroll]
        if (args.Length < 3
            || !double.TryParse(args[1], out var modelExp)
            || !double.TryParse(args[2], out var openTotal))
        {
            Console.Error.WriteLine("usage: advise-mls <modelExpectedGoals> <openTotal> [juice=-110] [bankroll]");
            Console.Error.WriteLine(
                $"MLS paper: w={MlsChampConfig.MarketWeight} |exp-line|>={MlsChampConfig.MinAbsTotalEdge} EV>={MlsChampConfig.MinEv:P0}");
            return 1;
        }
        var juice = args.Length > 3 && int.TryParse(args[3], out var j) ? j : -110;
        var bankroll = args.Length > 4 && double.TryParse(args[4], out var b) ? b : MlsChampConfig.BankrollSeed;
        var exp = MlsChampConfig.MarketWeight * openTotal + (1 - MlsChampConfig.MarketWeight) * modelExp;
        var edge = exp - openTotal;
        if (Math.Abs(edge) < MlsChampConfig.MinAbsTotalEdge)
        {
            Console.WriteLine("NO-GO");
            Console.WriteLine($"edge={edge:F2} goals < min {MlsChampConfig.MinAbsTotalEdge}");
            return 3;
        }
        var betOver = edge > 0;
        // advise-mls has only total μ — split evenly for Poisson path
        var half = modelExp / 2.0;
        var pOver = SoccerOverProbConfig.OverProb(half, half, exp, openTotal, "MLS");
        var p = betOver ? pOver : 1 - pOver;
        var advice = BetDecider.Advise(p, juice, bankroll, MlsChampConfig.MinEv);
        Console.WriteLine(advice.Go ? "GO" : "NO-GO");
        Console.WriteLine($"sport=MLS side={(betOver ? "OVER" : "UNDER")} total={openTotal:F1} edge={edge:F2} EV={advice.Ev:P2}");
        Console.WriteLine($"stakeFrac={advice.StakeFraction:P2}  {advice.Reason}");
        return advice.Go ? 0 : 3;
    }
    case "advise-ats":
    {
        if (args.Length < 3
            || !double.TryParse(args[1], out var p)
            || !double.TryParse(args[2], out var homeLine))
        {
            Console.Error.WriteLine("usage: advise-ats <homeWinProb 0-1> <homeSpreadLine> [juice=-110] [bankroll]");
            return 1;
        }
        var juice = args.Length > 3 && int.TryParse(args[3], out var j) ? j : -110;
        var bankroll = args.Length > 4 && double.TryParse(args[4], out var b) ? b : ChampConfig.BankrollSeed;
        var exp = SpreadMath.ExpectedHomeMargin(p);
        var edge = exp + homeLine;
        var sideHome = edge > 0;
        var pCover = sideHome ? SpreadMath.HomeCoverProb(exp, homeLine) : 1 - SpreadMath.HomeCoverProb(exp, homeLine);
        if (Math.Abs(edge) < ChampConfig.AtsMinAbsMarginEdge)
        {
            Console.WriteLine("NO-GO");
            Console.WriteLine($"edge={edge:F2} pts < min {ChampConfig.AtsMinAbsMarginEdge}");
            return 3;
        }
        var advice = BetDecider.Advise(pCover, juice, bankroll, ChampConfig.MinEv);
        Console.WriteLine(advice.Go ? "GO" : "NO-GO");
        Console.WriteLine($"side={(sideHome ? "HOME" : "AWAY")} edge={edge:F2} EV={advice.Ev:P2}  {advice.Reason}");
        return advice.Go ? 0 : 3;
    }
    case "paper-go":
    {
        // Independent DK + Kalshi + Robinhood + Crypto.com
        if (args.Length < 8
            || !double.TryParse(args[3], out var line)
            || !int.TryParse(args[4], out var odds)
            || !double.TryParse(args[5], out var modelProb)
            || !double.TryParse(args[6], out var absEdge))
        {
            Console.Error.WriteLine("usage: paper-go <gameKey> <OVER|UNDER|HOME|AWAY> <line> <dkOdds> <modelProb> <absEdge> <sport>");
            return 1;
        }
        if (OregonExclusion.IsExcludedGameKey(args[7], args[1]))
        {
            Console.Error.WriteLine($"EXCLUDED (Oregon rule): {args[1]} — never bet an Oregon-based team.");
            return 4;
        }
        var paperPath = PaperLedger.DefaultDbPath(FindRepoRoot());
        var (db, path) = await PaperLedger.OpenAsync(paperPath);
        await using (db)
        {
            var (dk, others) = await PaperLedger.PlaceDualVenueAsync(
                db, args[1], args[2], line, odds, modelProb, absEdge, args[7]);
            Console.WriteLine($"PAPER-GO {args[1]} {args[2]} {line} dk@{odds} p={modelProb:F3} edge={absEdge:F2} → {path}");
            Console.WriteLine($"  DK     placed={dk.Count}" + (dk.Count > 0 ? $" group={dk[0].TicketGroup}" : " (skip/already open/no EV)"));
            foreach (var r in dk)
                Console.WriteLine($"    #{r.Id} {r.Scheme,-10} ${r.StakeDollars:F2} @{r.AmericanOdds}");
            foreach (var o in others)
            {
                Console.WriteLine($"  {o.Venue,-8} placed={o.Placed.Count}" + (o.Placed.Count > 0 ? $" group={o.Placed[0].TicketGroup}" : ""));
                Console.WriteLine($"    {o.Note}");
                foreach (var r in o.Placed)
                    Console.WriteLine($"    #{r.Id} {r.Scheme,-10} ${r.StakeDollars:F2} @{r.AmericanOdds}");
            }
            if (dk.Count == 0 && others.All(x => x.Placed.Count == 0))
                Console.WriteLine("  (nothing placed on any venue)");
        }
        return 0;
    }
    case "paper-bet":
    {
        // Legacy single Flat lane: paper-bet <gameKey> <side> <line> <odds> <stake$> [modelProb]
        if (args.Length < 6
            || !double.TryParse(args[3], out var line)
            || !int.TryParse(args[4], out var odds)
            || !double.TryParse(args[5], out var stake))
        {
            Console.Error.WriteLine("usage: paper-bet <gameKey> <HOME|AWAY> <line> <odds> <stake$> [modelProb]");
            Console.Error.WriteLine("prefer: paper-go <key> <side> <line> <odds> <p> <absEdge> <sport>");
            return 1;
        }
        var modelProb = args.Length > 6 && double.TryParse(args[6], out var mp) ? mp : 0;
        if (OregonExclusion.IsExcludedGameKey(null, args[1]))
        {
            Console.Error.WriteLine($"EXCLUDED (Oregon rule): {args[1]} — never bet an Oregon-based team.");
            return 4;
        }
        var paperPath = PaperLedger.DefaultDbPath(FindRepoRoot());
        var (db, path) = await PaperLedger.OpenAsync(paperPath);
        await using (db)
        {
            var row = await PaperLedger.PlaceAsync(db, args[1], args[2], line, odds, stake, modelProb);
            Console.WriteLine($"PAPER #{row.Id} {row.Scheme} {row.GameKey} {row.Side} {row.Line:+0.0;-0.0} @{row.AmericanOdds} ${row.StakeDollars:F2} → {path}");
        }
        return 0;
    }
    case "settle-go":
    {
        // settle-go <gameKey|group> <W|L|P> [finalTotal] [closeLine] [closeOdds]
        // CloseLine is market close only; omit to auto-resolve from warehouse / line_snapshots.
        if (args.Length < 3)
        {
            Console.Error.WriteLine(
                "usage: settle-go <gameKey|ticketGroup> <W|L|P> [finalTotal] [closeLine] [closeOdds]");
            return 1;
        }
        double? finalTotal = args.Length > 3 && double.TryParse(args[3], out var ft) ? ft : null;
        double? closeLine = args.Length > 4 && double.TryParse(args[4], out var cl) ? cl : null;
        int? closeOdds = args.Length > 5 && int.TryParse(args[5], out var co) ? co : null;
        var root = FindRepoRoot();
        var paperPath = PaperLedger.DefaultDbPath(root);
        var warehouse = Path.Combine(root, "possiblymaybe.db");
        var snaps = Path.Combine(root, "data", "line_snapshots.db");
        var (db, _) = await PaperLedger.OpenAsync(paperPath);
        await using (db)
        {
            var probe = await db.LedgerEntries.AsNoTracking()
                .FirstOrDefaultAsync(r => r.Result == null
                    && (r.TicketGroup == args[1] || r.GameKey == args[1]));
            if (probe is not null && (finalTotal is null || closeLine is null))
            {
                var (autoClose, autoAm, autoFinal) = await PaperLedger.ResolveSettleLinesAsync(
                    warehouse, snaps, probe.GameKey, probe.Sport);
                finalTotal ??= autoFinal;
                if (closeLine is null)
                {
                    closeLine = autoClose;
                    closeOdds ??= autoAm;
                }
            }

            var rows = await PaperLedger.SettleGroupAsync(
                db, args[1], args[2], finalTotal, closeLine, closeOdds);
            Console.WriteLine(
                $"SETTLED-GO n={rows.Count} {args[1]} → {args[2]} final={finalTotal} close={closeLine}");
            foreach (var row in rows)
                Console.WriteLine(
                    $"  #{row.Id} {row.Scheme,-10} pnl={row.PnL:+0.00;-0.00} clvPts={row.ClvPts:+0.0;-0.0}");
        }
        return 0;
    }
    case "settle":
    {
        // settle <id> <W|L|P> [finalTotal] [closeLine] [closeOdds]
        if (args.Length < 3 || !int.TryParse(args[1], out var id))
        {
            Console.Error.WriteLine("usage: settle <id> <W|L|P> [finalTotal] [closeLine] [closeOdds]");
            return 1;
        }
        double? finalTotal = args.Length > 3 && double.TryParse(args[3], out var ft) ? ft : null;
        double? closeLine = args.Length > 4 && double.TryParse(args[4], out var cl) ? cl : null;
        int? closeOdds = args.Length > 5 && int.TryParse(args[5], out var co) ? co : null;
        var root = FindRepoRoot();
        var paperPath = PaperLedger.DefaultDbPath(root);
        var warehouse = Path.Combine(root, "possiblymaybe.db");
        var snaps = Path.Combine(root, "data", "line_snapshots.db");
        var (db, _) = await PaperLedger.OpenAsync(paperPath);
        await using (db)
        {
            var existing = await db.LedgerEntries.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id);
            if (existing is not null && (finalTotal is null || closeLine is null))
            {
                var (autoClose, autoAm, autoFinal) = await PaperLedger.ResolveSettleLinesAsync(
                    warehouse, snaps, existing.GameKey, existing.Sport);
                finalTotal ??= autoFinal;
                if (closeLine is null)
                {
                    closeLine = autoClose;
                    closeOdds ??= autoAm;
                }
            }

            var row = await PaperLedger.SettleAsync(db, id, args[2], finalTotal, closeLine, closeOdds);
            Console.WriteLine(
                $"SETTLED #{row.Id} {row.Scheme} {row.Result} pnl={row.PnL:+0.00;-0.00} final={row.FinalTotal} close={row.CloseLine} clvPts={row.ClvPts:+0.0;-0.0}");
        }
        return 0;
    }
    case "backfill-clv":
    {
        // Null fake CloseLine(=finals); refill from warehouse/tracker when knowable; set FinalTotal.
        var root = FindRepoRoot();
        var paperPath = args.Length > 1 ? args[1] : PaperLedger.DefaultDbPath(root);
        var warehouse = args.Length > 2 ? args[2] : Path.Combine(root, "possiblymaybe.db");
        var snaps = Path.Combine(root, "data", "line_snapshots.db");
        var (db, _) = await PaperLedger.OpenAsync(paperPath);
        await using (db)
        {
            var (n, with) = await PaperLedger.BackfillLiveClvAsync(db, warehouse, snaps);
            var pct = n == 0 ? 0.0 : 100.0 * with / n;
            Console.WriteLine($"BACKFILL-CLV settled={n} true-close={with} ({pct:0.#}%)");
        }
        return 0;
    }
    case "paper":
    {
        var paperPath = args.Length > 1 ? args[1] : PaperLedger.DefaultDbPath(FindRepoRoot());
        var (db, _) = await PaperLedger.OpenAsync(paperPath);
        await using (db)
            await PaperLedger.PrintStatusAsync(db);
        return 0;
    }
    case "dashboard":
    {
        var root = FindRepoRoot();
        var paperPath = args.Length > 1 ? args[1] : PaperLedger.DefaultDbPath(root);
        var (db, _) = await PaperLedger.OpenAsync(paperPath);
        await using (db)
        {
            await PaperLedger.MigrateLegacyAsync(db);
            await PaperLedger.WriteDashboardAsync(db, Path.Combine(root, "ui"));
        }
        return 0;
    }
    case "variants-shadow":
    {
        // Settle + evaluate slate day + write ui/variants.html (no paper confirmation).
        var root = FindRepoRoot();
        var warehouse = args.Length > 2 ? args[2] : Path.Combine(root, "possiblymaybe.db");
        var day = args.Length > 1 && args[1].Length == 8 ? args[1]
            : DateTime.UtcNow.ToString("yyyyMMdd");
        if (args.Length > 1 && args[1].EndsWith(".db", StringComparison.OrdinalIgnoreCase))
        {
            warehouse = args[1];
            day = DateTime.UtcNow.ToString("yyyyMMdd");
        }
        await VariantShadow.RunAsync(
            warehouse, VariantLedger.DefaultDbPath(root), Path.Combine(root, "ui"), day);
        return 0;
    }
    case "variants-backfill":
    {
        var root = FindRepoRoot();
        var warehouse = args.Length > 1 ? args[1] : Path.Combine(root, "possiblymaybe.db");
        var vdb = args.Length > 2 ? args[2] : VariantLedger.DefaultDbPath(root);
        if (!File.Exists(warehouse))
        {
            Console.Error.WriteLine($"DB not found: {warehouse}");
            return 2;
        }
        await VariantBackfill.RunAsync(warehouse, vdb);
        await VariantDashboard.WriteAsync(vdb, Path.Combine(root, "ui"));
        return 0;
    }
    case "variants-settle":
    {
        var root = FindRepoRoot();
        var warehouse = args.Length > 1 ? args[1] : Path.Combine(root, "possiblymaybe.db");
        var n = await VariantSettle.SettleOpenAsync(VariantLedger.DefaultDbPath(root), warehouse);
        Console.WriteLine($"variants-settle: {n}");
        return 0;
    }
    case "variants-dashboard":
    {
        var root = FindRepoRoot();
        await VariantDashboard.WriteAsync(VariantLedger.DefaultDbPath(root), Path.Combine(root, "ui"));
        return 0;
    }
    case "ensure-db":
    {
        var dbPath = args.Length > 1 ? args[1] : "possiblymaybe.db";
        await AppDbContext.EnsureCreatedAsync($"Data Source={dbPath}");
        Console.WriteLine($"Database ready at {dbPath}");
        return 0;
    }
    case "help":
    case "--help":
    case "-h":
        PrintUsage();
        return 0;
    default:
        Console.Error.WriteLine($"Unknown command: {command}");
        PrintUsage();
        return 1;
}

static void PrintSlice(Gate1aResult r)
{
    Console.WriteLine($"--- {r.Slice} ---");
    Console.WriteLine($"Games: {r.GamesScored}  Bets: {r.Bets} (ML={r.MlBets}, ATS={r.SpreadBets})  HitRate: {r.HitRate:P1}");
    if (r.ClvUnit == "mixed")
        Console.WriteLine($"CLV: ML mean={r.MeanMlClv:F4} (prob)  ATS mean={r.MeanSpreadClvPts:F3} (pts)");
    else
        Console.WriteLine($"Mean CLV ({r.ClvUnit}): {r.MeanClv:F4}");
    Console.WriteLine($"Paper PnL (u): {r.PaperPnlUnits:F1}  LogLoss: {r.LogLoss:F4}  Brier: {r.Brier:F4}");
}

static string ResolveRawRoot()
{
    var start = Directory.GetCurrentDirectory();
    for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
    {
        var candidate = Path.Combine(dir.FullName, "data", "raw");
        if (Directory.Exists(candidate))
            return candidate;
    }

    throw new DirectoryNotFoundException($"Could not find data/raw from cwd={start}. Run from repo root.");
}

static string FindRepoRoot()
{
    var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (dir is not null)
    {
        if (File.Exists(Path.Combine(dir.FullName, "PossiblyMaybe.sln")))
            return dir.FullName;
        dir = dir.Parent;
    }
    return Directory.GetCurrentDirectory();
}

static void PrintUsage()
{
    Console.WriteLine("""
        PossiblyMaybe CLI
          import [all] [db]          Load A/B/C/D(+E) into SQLite
          backtest [dbPath]          ML / spread / combo walk-forward report
          sweep [dbPath]             Train-only ATS sweep; holdout scored once for champ
          import-totals [db]         Load TOTAL open/close (B + E) into warehouse
          import-box [db]            Load pace box (nba_box/box_games.csv)
          refresh-results [yyyymmdd] [db]  Incremental finals for NBA/MLS/LIGUE1/LIGAMX/SERIEA (ESPN; NBA box, MLS xG)
          import-mlb / import-nfl / import-mls   Append MLB / NFL / MLS totals
          sweep-totals [db] [--normal-sigma]  NBA O/U; --normal-sigma = Tier A Normal σ
          import-round2-odds [db]    D1 flancast NFL O→C overlay + MLB 2011–21
          import-line-tracker [snapshots.db] [db]  Tracker open/close → OddsSnapshots
          import-disk-odds [db]      W15 on-disk SBRO NFL/NBA/NCAA + Arnav MLB O→C
          sweep-mlb / sweep-nfl / sweep-mls      Soft-spot sweeps (MLB / NFL / MLS)
          sweep-soccer-dc <SPORT> <hold> [db]  Dixon–Coles att-def EMA totals
          tier-a-poisson [SPORT|all] [db]  Tier A poisson-cdf-pover (pre-holdout folds)
          tier-a-guards [db]             Edge-cap + line-move Tier A + removed-bet proof
          tier-a-nfl-wind [db]           NFL nfl-wind alone Tier A (filled O→C)
          tier-a-shutoff [db]            V3 vs V4 rolling-CLV shutoff PnL / max DD
          sweep-decay [db]           EMA α half-life + sparse B2B (library sparsity)
          slate-nba [yyyymmdd] [bank]  Today's NBA totals soft spots (PaceEff)
          slate-mls [yyyymmdd] [bank]  Today's MLS totals soft spots (paper)
          slate-soccer <SPORT> [day] [bank]  MLS|LIGUE1|LIGAMX|SERIEA HOLD-PASS slates
          advise-ou <expTotal> <openTotal> [juice] [bank]  Champ TOTALS go/no-go
          advise-mls <expGoals> <openTotal> [juice] [bank] MLS paper TOTALS go/no-go
          advise-ats <p> <homeLine> [juice] [bank]           Legacy ATS go/no-go
          paper-go <key> <side> <line> <dkOdds> <p> <absEdge> <sport>
                                 Independent DK + Kalshi place (each venue EV-gates / sizes alone)
          settle-go <key|group> <W|L|P> [finalTotal] [closeLine] [closeOdds]
                                 final=score total; close=market only (auto from warehouse/tracker)
          paper-bet …              Legacy single Flat lane
          settle <id> <W|L|P> …    Settle one ledger id
          backfill-clv [paper.db] [warehouse.db]  Null fake closes; refill true CloseLine + FinalTotal
          paper [paper.db]         Leaderboard of all stake lanes
          dashboard                Write ui/index.html equity + leaderboard
          variants-shadow [yyyymmdd] Settle + eval slate → data/variants.db + ui/variants.html
          variants-backfill [db]     Walk-forward + replay week into shadow ledger
          variants-settle [db]       Auto-settle open shadow picks (no confirm)
          variants-dashboard         Write ui/variants.html
          ensure-db [path]           Create SQLite schema only
          help                       Show this help
        """);
}
