using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Cli.Backtest;

/// <summary>
/// Round 5 T4c: one Tier A pass for B8 (+conformal, kalman) and B7 soccer-strict vs V0.
/// Durable <see cref="TrialLedger"/>; PASS → Shadow only.
/// </summary>
public static class T4cTierA
{
    public static async Task RunAsync(
        string dbPath,
        string vaultDbPath,
        CancellationToken ct = default)
    {
        Console.WriteLine("######## Round 5 T4c: Tier A once (B8 + soccer-strict) ########");
        Console.WriteLine($"Warehouse: {dbPath}");
        Console.WriteLine($"TrialLedger: {vaultDbPath}");
        Console.WriteLine("PASS → Shadow only. No paper-go / settle. OregonExclusion on.\n");

        var (_, trials) = VaultDb.Open(vaultDbPath);
        var nBefore = trials.NTrials;

        var (cfPass, kalPass) = await HigherRiskTierA.RunAsync(dbPath, trials, ct);
        Console.WriteLine();
        var strictPass = await SoccerStrictTierA.RunAsync(dbPath, trials, ct: ct);

        Console.WriteLine();
        Console.WriteLine("######## T4c summary ########");
        Console.WriteLine($"+conformal:     {(cfPass ? "PASS → Shadow" : "FAIL → Shadow marked FAIL")}");
        Console.WriteLine($"kalman:         {(kalPass ? "PASS → Shadow" : "FAIL → Shadow marked FAIL")}");
        Console.WriteLine($"soccer-strict:  {(strictPass ? "PASS → Shadow" : "FAIL → Shadow marked FAIL")}");
        Console.WriteLine($"TrialLedger ΔN_trials={trials.NTrials - nBefore}  total N_trials={trials.NTrials}");
        Console.WriteLine(trials.FormatReportFooter());
        Console.WriteLine("No paper promotion from this table.");
    }
}
