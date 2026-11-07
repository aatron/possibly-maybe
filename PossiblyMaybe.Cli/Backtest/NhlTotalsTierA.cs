using PossiblyMaybe.Cli.Replay;
using PossiblyMaybe.Domain;
using PossiblyMaybe.Domain.SportPacks;

namespace PossiblyMaybe.Cli.Backtest;

/// <summary>
/// T6 / C6-nhl Tier A walk-forward once + look-once Tier C vault burn (T1 durable).
/// Verdicts feed shadow / radar only — never paper from this table.
/// CLI: tier-a-nhl [db]
/// </summary>
public static class NhlTotalsTierA
{
    public const int NTrials = 1;
    public const string StrategyId = "nhl-goals-ema";

    /// <summary>Calendar day inside Tier C (Jan 2022 → InferSeason 2021-2022).</summary>
    public const string TierCVaultDay = "20220115";

    public static async Task RunAsync(string dbPath, string? vaultDbPath = null, CancellationToken ct = default)
    {
        var pack = NhlSportPack.Instance;
        if (!NhlSportPack.TierAReady)
        {
            Console.WriteLine("VERDICT NHL TierA=FAIL (TierAReady=false — no O→C folds declared)");
            return;
        }

        Console.WriteLine("######## Tier A: NHL goals-EMA (C6-nhl / T6) ########");
        Console.WriteLine($"Gates frozen: w={pack.MarketWeight} |exp−line|≥{pack.MinAbsTotalEdge} EV≥{pack.MinEv:P0} α={pack.EmaAlpha}");
        Console.WriteLine($"TierAReady={NhlSportPack.TierAReady}. Folds: {string.Join(",", pack.TierAFolds)}");
        Console.WriteLine($"TrainMax={pack.TrainMaxSeason} Skip={pack.SkipSeason}. Tier C look-once={pack.TierCHoldoutSeason} (vault burn). N_trials={NTrials}");
        Console.WriteLine("Shadow/radar only — no paper promotion from this table.\n");
        Console.WriteLine($"{"fold",-12} {"n",5} {"clv",7} {"pnl",7} {"hit",7}");

        var foldWins = 0;
        var nTot = 0;
        double sumClv = 0, sumPnl = 0;

        foreach (var fold in pack.TierAFolds)
        {
            var r = await NhlTotalsBacktest.RunSeasonFoldAsync(dbPath, fold, ct);
            nTot += r.Bets;
            sumClv += r.MeanClv * r.Bets;
            sumPnl += r.PaperPnlUnits;
            if (r.PaperPnlUnits > 0) foldWins++;
            Console.WriteLine(
                $"{fold,-12} {r.Bets,5} {r.MeanClv,7:F2} {r.PaperPnlUnits,7:F1} {r.HitRate,7:P1}");
        }

        var meanClv = nTot == 0 ? 0 : sumClv / nTot;
        var majority = foldWins * 2 > pack.TierAFolds.Count;
        var clvOk = meanClv > 0;
        var nOk = nTot >= 40;
        var tierA = majority && clvOk && nOk;

        Console.WriteLine();
        Console.WriteLine(
            $"SUMMARY folds={pack.TierAFolds.Count} winFolds={foldWins}/{pack.TierAFolds.Count} n={nTot} meanClv={meanClv:F3} pnl={sumPnl:F1}");
        Console.WriteLine(
            $"VERDICT NHL TierA={(tierA ? "PASS" : "FAIL")} (majorityPnL={majority}, meanCLV>0={clvOk}, n≥40={nOk}) N_trials={NTrials}");

        // Tier C look-once via T1 durable vault (--sim --final semantics).
        // Prefer cwd worktree root so burns land in this checkout's data/vault.db.
        var root = FindRepoRoot() ?? FindRepoRootNear(dbPath);
        var vaultPath = vaultDbPath ?? VaultDb.DefaultPath(root);
        var (vault, trials) = VaultDb.Open(vaultPath);
        var hash = Vault.StrategyHash(StrategyId, $"w={pack.MarketWeight};edge={pack.MinAbsTotalEdge};ev={pack.MinEv}");
        var opts = EngineRunOptions.Sim(hash, vault, trials, final: true,
            paramsJson: StrategyId, rangeFrom: TierCVaultDay, rangeTo: TierCVaultDay);

        Console.WriteLine($"\n######## Tier C look-once ({pack.TierCHoldoutSeason}; vault --sim --final) ########");
        Console.WriteLine($"vault={vaultPath} hash={hash} day={TierCVaultDay}");

        Gate1aResult hold;
        try
        {
            // Authorize + trial ledger before scoring so a crash mid-score still seals the burn.
            var slate = new List<SlateCandidate>
            {
                new("NHL", TierCVaultDay, "BOS", "NYR", 6.0, 3.0, 3.0,
                    6.0, 6.0, -110, -110, 0,
                    pack.MarketWeight, pack.MinAbsTotalEdge, pack.MinEv, false),
            };
            var view = new AsOfView(DateTimeOffset.Parse("2022-01-15T00:00:00Z"));
            var engine = DayEngine.Run(TierCVaultDay, StrategyRegistry.V0, view, slate, opts);
            Console.WriteLine($"vault burn ok: {engine.TrialFooter}");

            hold = await NhlTotalsBacktest.RunSeasonFoldAsync(dbPath, pack.TierCHoldoutSeason, ct);
        }
        catch (VaultAlreadyUsedException ex)
        {
            Console.WriteLine($"Vault already used — refuse second Tier C look: {ex.Message}");
            Console.WriteLine("VERDICT NHL TierC=REFUSED (already burned)");
            return;
        }

        Console.WriteLine(
            $"{pack.TierCHoldoutSeason} n={hold.Bets} clv={hold.MeanClv:F2} pnl={hold.PaperPnlUnits:F1} hit={hold.HitRate:P1}");
        var tierC = hold.Bets >= 40 && hold.MeanClv > 0 && hold.PaperPnlUnits > 0;
        Console.WriteLine(
            tierC
                ? "VERDICT NHL TierC=PASS (look-once report; shadow/radar only)"
                : "VERDICT NHL TierC=FAIL (look-once report; shadow/radar only)");
        Console.WriteLine("Do not place bets from this agent.");
    }

    static string? FindRepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "PossiblyMaybe.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }

    static string FindRepoRootNear(string dbPath)
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
}
