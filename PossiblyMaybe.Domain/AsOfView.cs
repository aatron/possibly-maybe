using System.Security.Cryptography;
using System.Text;

namespace PossiblyMaybe.Domain;

/// <summary>Scored game visible at a point in time (tipoff before as-of, scores present).</summary>
public sealed record FinalGameRow(
    int Id,
    string Sport,
    string? Season,
    DateTimeOffset TipoffUtc,
    string HomeTeam,
    string AwayTeam,
    int HomeScore,
    int AwayScore);

/// <summary>Odds row visible when CapturedAt ≤ as-of.</summary>
public sealed record OddsAsOfRow(
    int Id,
    int GameId,
    string Book,
    string Market,
    string Side,
    int AmericanOdds,
    double? Line,
    DateTimeOffset CapturedAt);

/// <summary>Injury / roster payload visible when CapturedAt ≤ as-of.</summary>
public sealed record InjuryRosterRow(
    string Sport,
    string Team,
    DateTimeOffset CapturedAt,
    string PayloadJson);

/// <summary>
/// Point-in-time data a strategy may see (R7). Sealed; no DB handle.
/// Construct via <c>AsOfViewFactory</c> (Cli) or tests — never hand a DbContext to a strategy.
/// </summary>
public sealed class AsOfView
{
    readonly FinalGameRow[] _finals;
    readonly OddsAsOfRow[] _odds;
    readonly InjuryRosterRow[] _injuries;

    public DateTimeOffset AsOf { get; }
    public string? SeasonPhase { get; }

    public AsOfView(
        DateTimeOffset asOf,
        IEnumerable<FinalGameRow>? finals = null,
        IEnumerable<OddsAsOfRow>? odds = null,
        IEnumerable<InjuryRosterRow>? injuries = null,
        string? seasonPhase = null)
    {
        AsOf = asOf;
        SeasonPhase = seasonPhase;
        // Defensive re-filter: even a buggy factory cannot leak future rows through the view.
        _finals = (finals ?? [])
            .Where(g => g.TipoffUtc < asOf)
            .OrderBy(g => g.TipoffUtc).ThenBy(g => g.Id)
            .ToArray();
        _odds = (odds ?? [])
            .Where(o => o.CapturedAt <= asOf)
            .OrderBy(o => o.CapturedAt).ThenBy(o => o.Id)
            .ToArray();
        _injuries = (injuries ?? [])
            .Where(i => i.CapturedAt <= asOf)
            .OrderBy(i => i.CapturedAt).ThenBy(i => i.Sport).ThenBy(i => i.Team)
            .ToArray();
    }

    public IReadOnlyList<FinalGameRow> FinalGames(string? sport = null)
        => sport is null ? _finals : _finals.Where(g => g.Sport == sport).ToArray();

    public IReadOnlyList<OddsAsOfRow> OddsSnapshots(int? gameId = null)
        => gameId is null ? _odds : _odds.Where(o => o.GameId == gameId.Value).ToArray();

    public IReadOnlyList<InjuryRosterRow> InjuryRoster(string? sport = null, string? team = null)
    {
        IEnumerable<InjuryRosterRow> q = _injuries;
        if (sport is not null) q = q.Where(i => i.Sport == sport);
        if (team is not null) q = q.Where(i => i.Team == team);
        return sport is null && team is null ? _injuries : q.ToArray();
    }

    /// <summary>Stable fingerprint of everything visible at AsOf (leak / replay identity).</summary>
    public string Watermark()
    {
        var sb = new StringBuilder();
        sb.Append(AsOf.UtcDateTime.ToString("o")).Append('|').Append(SeasonPhase ?? "");
        foreach (var g in _finals)
            sb.Append("|G:").Append(g.Id).Append(':').Append(g.HomeScore).Append('-').Append(g.AwayScore);
        foreach (var o in _odds)
            sb.Append("|O:").Append(o.Id).Append('@').Append(o.CapturedAt.UtcDateTime.ToString("o"));
        foreach (var i in _injuries)
            sb.Append("|I:").Append(i.Sport).Append('/').Append(i.Team).Append('@')
                .Append(i.CapturedAt.UtcDateTime.ToString("o")).Append('#').Append(i.PayloadJson.Length);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
