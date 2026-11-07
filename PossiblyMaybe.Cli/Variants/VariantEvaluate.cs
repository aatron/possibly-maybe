using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Cli.Variants;

/// <summary>Evaluate every active variant on shared slate candidates → data/variants.db.</summary>
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
            // Freeze pause map for this slate; signal = V3 settled shadow CLV.
            HashSet<string>? paused = null;
            if (VariantRegistry.All.Any(v => v.LeagueShutoff))
                paused = await VariantLedger.PausedLeaguesAsync(db, ct: ct);

            var n = 0;
            foreach (var c in candidates)
            {
                var key = VariantRegistry.GameKey(c);
                foreach (var v in VariantRegistry.All)
                {
                    var d = VariantRegistry.Evaluate(v, c, v.LeagueShutoff ? paused : null);
                    await VariantLedger.UpsertAsync(db, v.Id, c.Sport, key, d, c.OpenLine, c.Line, ct);
                    n++;
                }
            }
            return n;
        }
    }
}
