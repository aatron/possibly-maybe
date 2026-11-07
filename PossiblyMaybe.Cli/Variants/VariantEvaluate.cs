using Microsoft.EntityFrameworkCore;
using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Cli.Variants;

/// <summary>Persist slate candidates, then filter every active variant → data/variants.db.</summary>
public static class VariantEvaluate
{
    public static async Task<int> RecordAsync(
        string variantsDbPath,
        IReadOnlyList<SlateCandidate> candidates,
        CancellationToken ct = default)
    {
        if (candidates.Count == 0) return 0;
        var (db, _) = await VariantLedger.OpenAsync(variantsDbPath, ct);
        await using (db)
        {
            foreach (var c in candidates)
                await VariantLedger.UpsertCandidateAsync(db, c, ct);

            return await EvalAllAsync(db, candidates, ct);
        }
    }

    /// <summary>
    /// Re-filter every registered variant over stored candidates — no model re-run.
    /// Use after adding a wave-B family file.
    /// </summary>
    public static async Task<int> ReEvalStoredAsync(
        string variantsDbPath,
        CancellationToken ct = default)
    {
        var (db, _) = await VariantLedger.OpenAsync(variantsDbPath, ct);
        await using (db)
        {
            var rows = await db.CandidateEntries.AsNoTracking().ToListAsync(ct);
            var candidates = rows.Select(VariantLedger.ToSlate).ToList();
            if (candidates.Count == 0) return 0;
            return await EvalAllAsync(db, candidates, ct);
        }
    }

    static async Task<int> EvalAllAsync(
        VariantDbContext db,
        IReadOnlyList<SlateCandidate> candidates,
        CancellationToken ct)
    {
        // Freeze pause map for this slate; signal = V3 settled shadow CLV.
        HashSet<string>? paused = null;
        if (VariantRegistry.All.Any(v => v.LeagueShutoff))
            paused = await VariantLedger.PausedLeaguesAsync(db, ct: ct);

        var runId = Guid.NewGuid();
        var n = 0;
        foreach (var c in candidates)
        {
            // Totals launch-set only; SPREAD/ML candidates are stored, not shadowed here.
            if (!c.Market.Equals("TOTAL", StringComparison.OrdinalIgnoreCase))
                continue;
            var key = VariantRegistry.GameKey(c);
            foreach (var v in VariantRegistry.All)
            {
                var d = VariantRegistry.Evaluate(v, c, v.LeagueShutoff ? paused : null);
                await VariantLedger.UpsertAsync(db, v.Id, c.Sport, key, d, c.OpenLine, c.Line, ct);
                await VariantLedger.AppendDecisionAsync(db, runId, v.Id, c.Sport, key, d, c, ct: ct);
                n++;
            }
        }
        return n;
    }
}
