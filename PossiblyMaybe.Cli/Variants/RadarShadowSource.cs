using Microsoft.EntityFrameworkCore;
using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Cli.Variants;

/// <summary>
/// Reads settled shadow picks as <see cref="ClvObservation"/> from the current r3 schema.
/// Does not depend on A1 candidate-store changes.
/// </summary>
public static class RadarShadowSource
{
    public static async Task<IReadOnlyList<ClvObservation>> LoadAsync(
        VariantDbContext db, CancellationToken ct = default)
    {
        var rows = await db.ShadowEntries.AsNoTracking()
            .Where(e => e.Result != null && e.ClvPts != null && e.SkipReason == null)
            .Select(e => new { e.Sport, e.VariantId, e.ClvPts, e.SettledAt, e.EvaluatedAt })
            .ToListAsync(ct);

        return rows
            .Select(e => new ClvObservation(
                e.Sport,
                WaveRadar.DefaultMarket,
                e.VariantId,
                e.SettledAt ?? e.EvaluatedAt,
                e.ClvPts!.Value))
            .ToList();
    }

    public static async Task<IReadOnlyList<ClvObservation>> LoadFromPathAsync(
        string variantsDbPath, CancellationToken ct = default)
    {
        var (db, _) = await VariantLedger.OpenAsync(variantsDbPath, ct);
        await using (db)
            return await LoadAsync(db, ct);
    }
}
