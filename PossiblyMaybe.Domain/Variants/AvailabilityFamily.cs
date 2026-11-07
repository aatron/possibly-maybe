using System.Runtime.CompilerServices;
using System.Text.Json;

namespace PossiblyMaybe.Domain;

/// <summary>
/// Availability filters: AbsenceBook DNP adj (+absence) and live ESPN injury skip (+injury-guard).
/// </summary>
public static class AvailabilityFamily
{
    public const int Order = 100;
    public const string AbsenceId = "+absence";
    public const string InjuryGuardId = "+injury-guard";

    private static readonly HashSet<string> NbaOnly = new(StringComparer.OrdinalIgnoreCase) { "NBA" };

    private static readonly HashSet<string> InjurySports = new(StringComparer.OrdinalIgnoreCase)
    {
        "NBA", "NFL", "NCAAB", "NCAAF",
    };

    /// <summary>V0 stack + AbsenceBook tip-time DNP pts on <see cref="SlateCandidate.AbsenceAdjPts"/>.</summary>
    public static Variant Absence { get; } = new(
        AbsenceId, "+absence",
        NbaOverProbKind.NormalSigma,
        EdgeCap: false, EdgeCapEv: VariantRegistry.DefaultEdgeCapEv,
        LineMove: false, NbaLineMoveK: VariantRegistry.DefaultNbaLineMoveK,
        SoccerLineMoveK: VariantRegistry.DefaultSoccerLineMoveK,
        LeagueShutoff: false, UseRealJuice: false, Leagues: NbaOnly,
        AbsenceAdjust: true, InjuryGuard: false);

    /// <summary>V0 stack + skip when candidate InjuryFlag (live ESPN Out/Doubtful / 24h status change).</summary>
    public static Variant InjuryGuard { get; } = new(
        InjuryGuardId, "+injury-guard",
        NbaOverProbKind.NormalSigma,
        EdgeCap: false, EdgeCapEv: VariantRegistry.DefaultEdgeCapEv,
        LineMove: false, NbaLineMoveK: VariantRegistry.DefaultNbaLineMoveK,
        SoccerLineMoveK: VariantRegistry.DefaultSoccerLineMoveK,
        LeagueShutoff: false, UseRealJuice: false, Leagues: InjurySports,
        AbsenceAdjust: false, InjuryGuard: true);

#pragma warning disable CA2255 // family self-register so wave-B is one file
    [ModuleInitializer]
    internal static void Init() => VariantRegistry.Register(Order, Absence, InjuryGuard);
#pragma warning restore CA2255

    /// <summary>Reuse <see cref="AbsenceBook"/> — pts to store on <see cref="SlateCandidate.AbsenceAdjPts"/>.</summary>
    public static double AbsenceAdjPts(
        AbsenceBook book, IEnumerable<AbsenceBook.PlayerLine> tipLines, double scale = 0.5)
        => book.MissingPtsAdjustment(tipLines, scale);

    // ESPN site API paths (NBA / NFL / NCAA). Empty feed → live tests skip.
    private static readonly Dictionary<string, string> EspnInjuryPath = new(StringComparer.OrdinalIgnoreCase)
    {
        ["NBA"] = "basketball/nba",
        ["NFL"] = "football/nfl",
        ["NCAAB"] = "basketball/mens-college-basketball",
        ["NCAAF"] = "football/college-football",
    };

    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(20),
        DefaultRequestHeaders = { { "User-Agent", "PossiblyMaybe/4 (research; +injury-guard)" } },
    };

    /// <summary>
    /// Pull ESPN injuries; mark candidates whose home/away team has Out/Doubtful or a status
    /// dated within the last 24h. Returns how many rows were flagged. Empty/failed feed → 0.
    /// </summary>
    public static async Task<int> ApplyEspnInjuryFlagsAsync(
        IList<SlateCandidate> candidates, DateTimeOffset? now = null, CancellationToken ct = default)
    {
        if (candidates.Count == 0) return 0;
        var asOf = now ?? DateTimeOffset.UtcNow;
        var flagged = 0;
        foreach (var sport in candidates.Select(c => c.Sport).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!EspnInjuryPath.TryGetValue(sport, out var path)) continue;
            var report = await TryFetchInjuryReportAsync(path, ct);
            if (report.Count == 0) continue;

            for (var i = 0; i < candidates.Count; i++)
            {
                var c = candidates[i];
                if (!c.Sport.Equals(sport, StringComparison.OrdinalIgnoreCase)) continue;
                if (!TeamHasMaterialInjury(report, c.Home, c.Away, asOf)) continue;
                candidates[i] = c with { InjuryFlag = true };
                flagged++;
            }
        }

        return flagged;
    }

    /// <summary>Parse ESPN injuries JSON → team displayName → rows. Empty on bad JSON.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<InjuryRow>> ParseInjuryReport(string json)
    {
        var map = new Dictionary<string, IReadOnlyList<InjuryRow>>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(json)) return map;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("injuries", out var teams)) return map;
            foreach (var team in teams.EnumerateArray())
            {
                var name = team.TryGetProperty("displayName", out var dn) ? dn.GetString() ?? "" : "";
                if (name.Length == 0) continue;
                var rows = new List<InjuryRow>();
                if (team.TryGetProperty("injuries", out var list))
                {
                    foreach (var inj in list.EnumerateArray())
                    {
                        var status = inj.TryGetProperty("status", out var st) ? st.GetString() ?? "" : "";
                        DateTimeOffset? when = null;
                        if (inj.TryGetProperty("date", out var dt)
                            && DateTimeOffset.TryParse(dt.GetString(), out var parsed))
                            when = parsed;
                        rows.Add(new InjuryRow(status, when));
                    }
                }

                map[name] = rows;
            }
        }
        catch (JsonException)
        {
            return new Dictionary<string, IReadOnlyList<InjuryRow>>(StringComparer.OrdinalIgnoreCase);
        }

        return map;
    }

    public readonly record struct InjuryRow(string Status, DateTimeOffset? Date);

    public static bool IsMaterialStatus(string status)
    {
        if (string.IsNullOrWhiteSpace(status)) return false;
        // Out / Doubtful / Day-To-Day count; "Probable" alone does not.
        return status.Contains("Out", StringComparison.OrdinalIgnoreCase)
               || status.Contains("Doubtful", StringComparison.OrdinalIgnoreCase)
               || status.Contains("Day-To-Day", StringComparison.OrdinalIgnoreCase)
               || status.Equals("DTD", StringComparison.OrdinalIgnoreCase);
    }

    public static bool TeamHasMaterialInjury(
        IReadOnlyDictionary<string, IReadOnlyList<InjuryRow>> report,
        string home, string away, DateTimeOffset asOf)
    {
        foreach (var (teamName, rows) in report)
        {
            if (!TeamMatch(teamName, home) && !TeamMatch(teamName, away)) continue;
            foreach (var row in rows)
            {
                if (!IsMaterialStatus(row.Status)) continue;
                if (row.Date is { } d && asOf - d <= TimeSpan.FromHours(24))
                    return true;
                // No date but Out/Doubtful still flags (can't prove "last 24h" without a stamp).
                if (row.Date is null
                    && (row.Status.Contains("Out", StringComparison.OrdinalIgnoreCase)
                        || row.Status.Contains("Doubtful", StringComparison.OrdinalIgnoreCase)))
                    return true;
            }
        }

        return false;
    }

    static bool TeamMatch(string espnTeam, string slateTeam)
    {
        if (espnTeam.Equals(slateTeam, StringComparison.OrdinalIgnoreCase)) return true;
        var espnCode = TeamCodes.TryNormalize(espnTeam);
        var slateCode = TeamCodes.TryNormalize(slateTeam) ?? slateTeam;
        return espnCode is not null
               && espnCode.Equals(slateCode, StringComparison.OrdinalIgnoreCase);
    }

    static async Task<IReadOnlyDictionary<string, IReadOnlyList<InjuryRow>>> TryFetchInjuryReportAsync(
        string path, CancellationToken ct)
    {
        try
        {
            var url = $"https://site.api.espn.com/apis/site/v2/sports/{path}/injuries";
            using var resp = await Http.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode) return EmptyReport();
            var json = await resp.Content.ReadAsStringAsync(ct);
            return ParseInjuryReport(json);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return EmptyReport();
        }
    }

    static IReadOnlyDictionary<string, IReadOnlyList<InjuryRow>> EmptyReport()
        => new Dictionary<string, IReadOnlyList<InjuryRow>>(StringComparer.OrdinalIgnoreCase);
}
