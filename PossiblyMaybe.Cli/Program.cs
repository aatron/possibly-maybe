using PossiblyMaybe.Cli.Backtest;
using PossiblyMaybe.Cli.Import;
using PossiblyMaybe.Cli.Paper;
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
        if (args.Any(a => a is "--cal" or "cal"))
            await TotalsSweep.CompareChampGatesAsync(dbPath);
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
        var p = betOver ? TotalsMath.OverProb(exp, openTotal) : 1 - TotalsMath.OverProb(exp, openTotal);
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
        var p = betOver
            ? TotalsMath.OverProb(exp, openTotal, sharpness: 1.2)
            : 1 - TotalsMath.OverProb(exp, openTotal, sharpness: 1.2);
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
    case "paper-bet":
    {
        // paper-bet <gameKey> <HOME|AWAY> <line> <odds> <stake$> [modelProb]
        if (args.Length < 6
            || !double.TryParse(args[3], out var line)
            || !int.TryParse(args[4], out var odds)
            || !double.TryParse(args[5], out var stake))
        {
            Console.Error.WriteLine("usage: paper-bet <gameKey> <HOME|AWAY> <line> <odds> <stake$> [modelProb]");
            return 1;
        }
        var modelProb = args.Length > 6 && double.TryParse(args[6], out var mp) ? mp : 0;
        var paperPath = PaperLedger.DefaultDbPath(FindRepoRoot());
        var (db, path) = await PaperLedger.OpenAsync(paperPath);
        await using (db)
        {
            var row = await PaperLedger.PlaceAsync(db, args[1], args[2], line, odds, stake, modelProb);
            Console.WriteLine($"PAPER #{row.Id} {row.GameKey} {row.Side} {row.Line:+0.0;-0.0} @{row.AmericanOdds} ${row.StakeDollars:F2} → {path}");
        }
        return 0;
    }
    case "settle":
    {
        // settle <id> <W|L|P> [closeLine] [closeOdds]
        if (args.Length < 3 || !int.TryParse(args[1], out var id))
        {
            Console.Error.WriteLine("usage: settle <id> <W|L|P> [closeLine] [closeOdds]");
            return 1;
        }
        double? closeLine = args.Length > 3 && double.TryParse(args[3], out var cl) ? cl : null;
        int? closeOdds = args.Length > 4 && int.TryParse(args[4], out var co) ? co : null;
        var paperPath = PaperLedger.DefaultDbPath(FindRepoRoot());
        var (db, _) = await PaperLedger.OpenAsync(paperPath);
        await using (db)
        {
            var row = await PaperLedger.SettleAsync(db, id, args[2], closeLine, closeOdds);
            Console.WriteLine($"SETTLED #{row.Id} {row.Result} pnl={row.PnL:+0.00;-0.00} clvPts={row.ClvPts:+0.0;-0.0}");
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
          import-mlb / import-nfl / import-mls   Append MLB / NFL / MLS totals
          sweep-totals [db]          NBA O/U + ISLP cal same-gate vs PaceEff champ
          sweep-mlb / sweep-nfl / sweep-mls      Soft-spot sweeps (MLB / NFL / MLS)
          sweep-soccer-dc <SPORT> <hold> [db]  Dixon–Coles att-def EMA totals
          sweep-decay [db]           EMA α half-life + sparse B2B (library sparsity)
          slate-nba [yyyymmdd] [bank]  Today's NBA totals soft spots (PaceEff)
          slate-mls [yyyymmdd] [bank]  Today's MLS totals soft spots (paper)
          advise-ou <expTotal> <openTotal> [juice] [bank]  Champ TOTALS go/no-go
          advise-mls <expGoals> <openTotal> [juice] [bank] MLS paper TOTALS go/no-go
          advise-ats <p> <homeLine> [juice] [bank]           Legacy ATS go/no-go
          paper-bet <key> <OVER|UNDER|HOME|AWAY> <line> <odds> <$> [p]
          settle <id> <W|L|P> [closeLine] [closeOdds]         Settle + CLV pts
          paper [paper.db]           Paper bankroll / CLV status (Gate 2)
          ensure-db [path]           Create SQLite schema only
          help                       Show this help
        """);
}
