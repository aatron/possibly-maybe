using PossiblyMaybe.Cli.Paper;
using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Cli.Variants;

/// <summary>
/// One-shot for pm-calculate: settle shadow picks, evaluate today's boards into variants.db, refresh ui/variants.html.
/// </summary>
public static class VariantShadow
{
    public static async Task RunAsync(
        string warehouseDbPath,
        string variantsDbPath,
        string uiDir,
        string yyyymmdd,
        CancellationToken ct = default)
    {
        var settled = await VariantSettle.SettleOpenAsync(variantsDbPath, warehouseDbPath, ct);
        Console.WriteLine($"variants-shadow: settled {settled}");

        var candidates = new List<SlateCandidate>();
        candidates.AddRange(await DailySlate.BuildNbaCandidatesAsync(warehouseDbPath, yyyymmdd, ct));
        try
        {
            candidates.AddRange(await DailySlate.BuildNcaabCandidatesAsync(warehouseDbPath, yyyymmdd, ct));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"variants-shadow: NCAAB scoreboard skipped ({ex.Message})");
        }
        foreach (var sport in DailySlate.SoccerEspnPaths.Keys)
            candidates.AddRange(await DailySlate.BuildSoccerCandidatesAsync(sport, warehouseDbPath, yyyymmdd, ct));
        try
        {
            candidates.AddRange(await DailySlate.BuildNhlCandidatesAsync(warehouseDbPath, yyyymmdd, ct));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"variants-shadow: NHL scoreboard skipped ({ex.Message})");
        }

        var n = await VariantEvaluate.RecordAsync(variantsDbPath, candidates, ct);
        Console.WriteLine($"variants-shadow: recorded {n} variant×game rows from {candidates.Count} candidates ({yyyymmdd})");

        await VariantDashboard.WriteAsync(variantsDbPath, uiDir, ct: ct);
    }
}
