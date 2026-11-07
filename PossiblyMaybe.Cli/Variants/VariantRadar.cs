using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Cli.Variants;

/// <summary>
/// <c>variants-radar</c>: scan shadow CLV → BH table + wave states → data/radar.db + dashboard waves.
/// </summary>
public static class VariantRadar
{
    public static async Task<IReadOnlyList<WaveCellResult>> RunAsync(
        string variantsDbPath,
        string radarDbPath,
        string? uiDir = null,
        CancellationToken ct = default)
    {
        var obs = await RadarShadowSource.LoadFromPathAsync(variantsDbPath, ct);
        var (rdb, _) = await RadarStore.OpenAsync(radarDbPath, ct);
        await using (rdb)
        {
            var prior = await RadarStore.LoadStagesAsync(rdb, ct);
            var results = WaveRadar.Scan(obs, prior);
            await RadarStore.SaveAsync(rdb, results, ct);
            PrintTable(results);
            WriteSummaryLine(results);

            if (!string.IsNullOrEmpty(uiDir))
                await VariantDashboard.WriteAsync(variantsDbPath, uiDir, radarDbPath, ct);

            return results;
        }
    }

    public static void PrintTable(IReadOnlyList<WaveCellResult> results)
    {
        var nTrials = results.Count == 0 ? 0 : results[0].NTrials;
        Console.WriteLine($"variants-radar: {results.Count} cells  N_trials={nTrials}  BH q={WaveRadar.BhQ:0.00}");
        Console.WriteLine(
            $"{"League",-10} {"Mkt",-6} {"Var",-8} {"n",4} {"CLV±SE",14} {"roll",6} {"CUSUM",6} {"p",8} {"BH",3} {"Wave",5} {"Ladder",14} {"Ckpt",12} {"verdict",9} {"wk",3}");
        foreach (var r in results.OrderByDescending(x => x.Wave).ThenBy(x => x.PValue))
        {
            var clv = $"{r.MeanClv:0.00}±{r.ClvSe:0.00}";
            Console.WriteLine(
                $"{r.League,-10} {r.Market,-6} {r.VariantId,-8} {r.N,4} {clv,14} {r.RollingClv,6:0.00} {r.Cusum,6:0.0} {r.PValue,8:0.0000} {(r.BhSignificant ? "Y" : "."),3} {r.Wave,5} {r.Stage,14} {r.Checkpoint,12} {r.CheckpointVerdict,9} {r.WeeksOn,3}");
        }

        var on = results.Count(r => r.Wave == WaveState.On);
        var bh = results.Count(r => r.BhSignificant);
        var paper = results.Count(r => r.Stage == LadderStage.Paper);
        var promo = results.Count(r => r.PromoteToPaperEligible);
        var demote = results.Count(r => r.Demote);
        var wide = results.Count(r => r.AllowsWideStake);
        Console.WriteLine(
            $"waves: ON={on} BH_sig={bh} paper={paper} promote_eligible={promo} demote={demote} wide_stake={wide} N_trials={nTrials}");
    }

    /// <summary>One-liner for pm-calculate / logs.</summary>
    public static string FormatSummary(IReadOnlyList<WaveCellResult> results)
    {
        if (results.Count == 0)
            return "waves: (empty — no settled shadow CLV yet) N_trials=0";
        var nTrials = results[0].NTrials;
        var on = results.Count(r => r.Wave == WaveState.On);
        var bh = results.Count(r => r.BhSignificant);
        var paper = results.Count(r => r.Stage is LadderStage.Paper or LadderStage.RealCandidate);
        var wide = results.Count(r => r.AllowsWideStake);
        var top = results
            .Where(r => r.Wave == WaveState.On || r.BhSignificant)
            .OrderBy(r => r.PValue)
            .Take(3)
            .Select(r => $"{r.League}/{r.VariantId}:{r.Checkpoint}/{r.CheckpointVerdict}")
            .ToList();
        var tops = top.Count == 0 ? "—" : string.Join(", ", top);
        return $"waves: ON={on} BH_sig={bh} paper/real={paper} wide_stake={wide} N_trials={nTrials} top=[{tops}]";
    }

    static void WriteSummaryLine(IReadOnlyList<WaveCellResult> results)
        => Console.WriteLine(FormatSummary(results));
}
