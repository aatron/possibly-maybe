using System.Globalization;

namespace PossiblyMaybe.Domain;

/// <summary>
/// Point-in-time cutoff for model builders (R1). Train only on tipoffs strictly before as-of.
/// </summary>
public static class PointInTime
{
    /// <summary>Keep items whose tipoff is &lt; asOf. Null asOf ⇒ passthrough.</summary>
    public static IEnumerable<T> AsOf<T>(
        IEnumerable<T> items, Func<T, DateTimeOffset> tipoff, DateTimeOffset? asOf)
        => asOf is null ? items : items.Where(x => tipoff(x) < asOf.Value);

    /// <summary>UTC midnight of a slate day — ratings exclude that day's tipoffs and later.</summary>
    public static DateTimeOffset SlateAsOf(string yyyymmdd)
    {
        var day = DateTime.ParseExact(yyyymmdd, "yyyyMMdd", CultureInfo.InvariantCulture,
            DateTimeStyles.None);
        return new DateTimeOffset(DateTime.SpecifyKind(day, DateTimeKind.Utc));
    }
}
