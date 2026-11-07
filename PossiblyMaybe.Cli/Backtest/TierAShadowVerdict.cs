using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Cli.Backtest;

/// <summary>
/// Round 5 T4: Tier A fold table → Shadow only. Never paper/real from this table.
/// </summary>
public static class TierAShadowVerdict
{
    public const int MinBets = 40;

    /// <summary>Pre-declared keep: mean fold CLV ≥ V0 and n≥40.</summary>
    public static bool Pass(double meanClvVariant, double meanClvV0, int totalBets)
        => meanClvVariant >= meanClvV0 - 1e-12 && totalBets >= MinBets;

    /// <summary>
    /// Print PASS→Shadow / FAIL→Shadow marked FAIL; refuse paper/real; append durable trial row.
    /// </summary>
    public static LadderStage Emit(
        TrialLedger trials,
        string variantId,
        bool pass,
        string paramsJson,
        string dateFrom,
        string dateTo,
        int gridSize = 1)
    {
        var hash = Vault.StrategyHash(variantId, paramsJson);
        trials.Record(hash, paramsJson, dateFrom, dateTo, gridSize);

        // Never request Paper/Real from this table — Shadow only (R10 / T4).
        if (pass)
        {
            Console.WriteLine($"VERDICT {variantId} PASS → Shadow (radar decides paper; no paper from this table)");
            return LadderStage.Shadow;
        }

        Console.WriteLine($"VERDICT {variantId} FAIL → Shadow marked FAIL (stays shadow; no paper)");
        return LadderStage.Shadow;
    }
}
