using Microsoft.EntityFrameworkCore;
using PossiblyMaybe.Cli.Backtest;
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
        // D1: CLV / promotion only from same-book true closes (never CrossBookClose / Consensus-as-close).
        var rows = await db.ShadowEntries.AsNoTracking()
            .Where(e => e.Result != null && e.ClvPts != null && e.CloseLine != null
                        && !e.CrossBookClose && e.SkipReason == null)
            .Select(e => new { e.Sport, e.VariantId, e.GameKey, e.ClvPts, e.SettledAt, e.EvaluatedAt })
            .ToListAsync(ct);

        var candPhases = await db.CandidateEntries.AsNoTracking()
            .Where(c => c.SeasonPhase != null)
            .Select(c => new { c.Sport, c.GameKey, c.SeasonPhase, c.Yyyymmdd })
            .ToListAsync(ct);
        var phaseByKey = candPhases.ToDictionary(
            c => (c.Sport, c.GameKey),
            c => c.SeasonPhase!,
            StringTupleComparer.Instance);

        return rows
            .Select(e =>
            {
                string? phase = null;
                if (phaseByKey.TryGetValue((e.Sport, e.GameKey), out var stored))
                    phase = stored;
                else if (SeasonalityPhases.TryParseGameKeyDay(e.GameKey, out var day))
                    phase = SeasonalityPhases.Resolve(e.Sport, day);
                return new ClvObservation(
                    e.Sport,
                    WaveRadar.DefaultMarket,
                    e.VariantId,
                    e.SettledAt ?? e.EvaluatedAt,
                    e.ClvPts!.Value,
                    phase);
            })
            .ToList();
    }

    sealed class StringTupleComparer : IEqualityComparer<(string Sport, string GameKey)>
    {
        public static readonly StringTupleComparer Instance = new();
        public bool Equals((string Sport, string GameKey) x, (string Sport, string GameKey) y)
            => x.Sport.Equals(y.Sport, StringComparison.OrdinalIgnoreCase)
               && x.GameKey.Equals(y.GameKey, StringComparison.OrdinalIgnoreCase);
        public int GetHashCode((string Sport, string GameKey) obj)
            => HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Sport),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.GameKey));
    }

    public static async Task<IReadOnlyList<ClvObservation>> LoadFromPathAsync(
        string variantsDbPath, CancellationToken ct = default)
    {
        var (db, _) = await VariantLedger.OpenAsync(variantsDbPath, ct);
        await using (db)
            return await LoadAsync(db, ct);
    }
}
