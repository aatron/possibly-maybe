using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PossiblyMaybe.Data;
using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Cli.Import;

/// <summary>
/// T3: backfill TOTAL open/close from ESPN core odds / summary pickcenter for the replay window.
/// Close CapturedAt = tipoff (AsOfView at decision time cannot see it). Open CapturedAt = tipoff − 7 days
/// (same as DiskOddsImporter — always before SlateAsOf of the tip day).
/// Single-line ESPN payloads are stored as CloseOnly; replay skips those games.
/// </summary>
public static class EspnLineBackfill
{
    public static readonly DateOnly DefaultFrom = new(2026, 10, 24);
    public static readonly DateOnly DefaultTo = new(2026, 11, 6);

    public static readonly string[] Leagues =
    [
        "NBA", "MLS", "LIGUE1", "LIGAMX", "SERIEA", "NFL", "NCAAF", "NCAAB", "NHL",
        "EPL", "BUNDESLIGA", "LALIGA", "EFL",
    ];

    static readonly HttpClient Http = new()
    {
        DefaultRequestHeaders = { { "User-Agent", "PossiblyMaybe/1.0" } },
        Timeout = TimeSpan.FromSeconds(30),
    };

    /// <summary>Site scoreboard path segment after /sports/.</summary>
    static readonly Dictionary<string, string> SitePath = new(StringComparer.OrdinalIgnoreCase)
    {
        ["NBA"] = "basketball/nba",
        ["NFL"] = "football/nfl",
        ["NCAAF"] = "football/college-football",
        ["NCAAB"] = "basketball/mens-college-basketball",
        ["NHL"] = "hockey/nhl",
        ["MLS"] = "soccer/usa.1",
        ["LIGUE1"] = "soccer/fra.1",
        ["LIGAMX"] = "soccer/mex.1",
        ["SERIEA"] = "soccer/ita.1",
        ["LALIGA"] = "soccer/esp.1",
        ["EFL"] = "soccer/eng.2",
        ["EPL"] = "soccer/eng.1",
        ["BUNDESLIGA"] = "soccer/ger.1",
    };

    /// <summary>Core API sport + league for /odds.</summary>
    static readonly Dictionary<string, (string Sport, string League)> CorePath = new(StringComparer.OrdinalIgnoreCase)
    {
        ["NBA"] = ("basketball", "nba"),
        ["NFL"] = ("football", "nfl"),
        ["NCAAF"] = ("football", "college-football"),
        ["NCAAB"] = ("basketball", "mens-college-basketball"),
        ["NHL"] = ("hockey", "nhl"),
        ["MLS"] = ("soccer", "usa.1"),
        ["LIGUE1"] = ("soccer", "fra.1"),
        ["LIGAMX"] = ("soccer", "mex.1"),
        ["SERIEA"] = ("soccer", "ita.1"),
        ["LALIGA"] = ("soccer", "esp.1"),
        ["EFL"] = ("soccer", "eng.2"),
        ["EPL"] = ("soccer", "eng.1"),
        ["BUNDESLIGA"] = ("soccer", "ger.1"),
    };

    static readonly Regex LineToken = new(@"[ou]?(\d+(?:\.\d+)?)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public sealed record LeagueStats(
        string Sport,
        int Games,
        int OpenFound,
        int CloseFound,
        int RealPrice,
        int ReplaySkipCloseOnly,
        int OregonExcluded,
        int UnmatchedEspn,
        int OddsFetchMiss);

    public sealed record RunResult(IReadOnlyList<LeagueStats> Leagues, int SnapsWritten);

    public readonly record struct ParsedTotal(
        double? OpenLine,
        int? OpenAmerican,
        double? CloseLine,
        int? CloseAmerican,
        string Provider);

    /// <summary>
    /// Replay must not bet without an open line. Close-only rows are for CLV after the fact.
    /// </summary>
    public static bool ReplayShouldSkip(IEnumerable<OddsSnapshot> totalSnaps)
        => !totalSnaps.Any(s => s.Market == "TOTAL" && s.PriceKind == PriceKind.Open);

    public static bool ReplayShouldSkip(IEnumerable<OddsAsOfRow> visibleOdds)
        => !visibleOdds.Any(o => o.Market == "TOTAL");

    public static async Task<RunResult> RunAsync(
        string dbPath,
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken ct = default)
    {
        var start = from ?? DefaultFrom;
        var end = to ?? DefaultTo;
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={dbPath}").Options;
        await using var db = new AppDbContext(options);

        // ET sports can tip after UTC midnight of the slate day — pull a ±1 day buffer then filter by KeyDate.
        // SQLite stores TipoffUtc as text; DateTimeOffset range predicates do not translate — filter in memory.
        var tipFrom = new DateTimeOffset(start.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).AddDays(-1);
        var tipTo = new DateTimeOffset(end.AddDays(2).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var leagueSet = new HashSet<string>(Leagues, StringComparer.OrdinalIgnoreCase);
        var warehouse = (await db.Games.ToListAsync(ct))
            .Where(g => leagueSet.Contains(g.Sport) && g.TipoffUtc >= tipFrom && g.TipoffUtc < tipTo)
            .ToList();

        // Attach missing EspnGameIds via scoreboard for sports that need it (NFL, sparse ids).
        await AttachEspnIdsAsync(db, warehouse, start, end, ct);

        var bySport = warehouse
            .Where(g =>
            {
                var d = KeyDate(g.Sport, g.TipoffUtc);
                return d >= start && d <= end;
            })
            .GroupBy(g => g.Sport, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var leagueStats = new List<LeagueStats>();
        var pending = new List<OddsSnapshot>();
        var touchIds = new HashSet<int>();

        foreach (var sport in Leagues)
        {
            bySport.TryGetValue(sport, out var games);
            games ??= [];
            var openFound = 0;
            var closeFound = 0;
            var realPrice = 0;
            var skipCloseOnly = 0;
            var oregon = 0;
            var fetchMiss = 0;

            using var gate = new SemaphoreSlim(8);
            var fetched = await Task.WhenAll(games.Select(async g =>
            {
                var oregonHit = OregonExclusion.IsExcluded(sport, g.HomeTeam, g.AwayTeam);
                if (string.IsNullOrWhiteSpace(g.EspnGameId))
                    return (g, oregonHit, Parsed: (ParsedTotal?)null, Miss: true);

                await gate.WaitAsync(ct);
                try
                {
                    var parsed = await FetchParsedTotalAsync(sport, g.EspnGameId, ct);
                    var miss = parsed is null
                               || (parsed.Value.OpenLine is null && parsed.Value.CloseLine is null);
                    return (g, oregonHit, Parsed: parsed, Miss: miss);
                }
                finally
                {
                    gate.Release();
                }
            }));

            foreach (var (g, oregonHit, parsed, miss) in fetched)
            {
                if (oregonHit) oregon++;
                if (miss || parsed is null)
                {
                    fetchMiss++;
                    continue;
                }

                var p = parsed.Value;
                var book = MapBook(p.Provider);
                touchIds.Add(g.Id);

                var openLine = p.OpenLine;
                var closeLine = p.CloseLine;
                var openAm = p.OpenAmerican;
                var closeAm = p.CloseAmerican;

                // One line only → store as close (CLV-only); replay skips.
                if (openLine is null && closeLine is { } onlyClose)
                {
                    pending.Add(Snap(g.Id, book, onlyClose, closeAm, PriceKind.Close, ClvQuality.CloseOnly, g.TipoffUtc));
                    closeFound++;
                    skipCloseOnly++;
                    if (closeAm is not null) realPrice++;
                    continue;
                }

                if (openLine is { } ol && closeLine is null)
                {
                    // PLAN: single line → close. Replay skips.
                    pending.Add(Snap(g.Id, book, ol, openAm, PriceKind.Close, ClvQuality.CloseOnly, g.TipoffUtc));
                    closeFound++;
                    skipCloseOnly++;
                    if (openAm is not null) realPrice++;
                    continue;
                }

                if (openLine is { } oLine && closeLine is { } cLine)
                {
                    pending.Add(Snap(g.Id, book, oLine, openAm, PriceKind.Open, ClvQuality.OpenClose,
                        g.TipoffUtc.AddDays(-7)));
                    pending.Add(Snap(g.Id, book, cLine, closeAm, PriceKind.Close, ClvQuality.OpenClose,
                        g.TipoffUtc));
                    openFound++;
                    closeFound++;
                    if (openAm is not null) realPrice++;
                    if (closeAm is not null) realPrice++;
                }
            }

            leagueStats.Add(new LeagueStats(
                sport, games.Count, openFound, closeFound, realPrice, skipCloseOnly, oregon, 0, fetchMiss));
        }

        if (touchIds.Count > 0)
        {
            var ids = touchIds.ToList();
            await db.OddsSnapshots
                .Where(o => ids.Contains(o.GameId) && o.Book == Book.DraftKings && o.Market == "TOTAL")
                .ExecuteDeleteAsync(ct);
        }

        if (pending.Count > 0)
        {
            db.OddsSnapshots.AddRange(pending);
            await db.SaveChangesAsync(ct);
        }

        Console.WriteLine($"backfill-espn-lines: window {start:yyyy-MM-dd}→{end:yyyy-MM-dd} snaps={pending.Count}");
        Console.WriteLine($"{"Sport",-12} {"Games",6} {"Open",6} {"Close",6} {"Real$",6} {"Skip",6} {"Miss",6}");
        foreach (var s in leagueStats)
        {
            Console.WriteLine(
                $"{s.Sport,-12} {s.Games,6} {s.OpenFound,6} {s.CloseFound,6} {s.RealPrice,6} {s.ReplaySkipCloseOnly,6} {s.OddsFetchMiss,6}");
        }

        var skipTotal = leagueStats.Sum(s => s.ReplaySkipCloseOnly);
        Console.WriteLine($"replay skip (close-only / no open): {skipTotal}");
        return new RunResult(leagueStats, pending.Count);
    }

    static OddsSnapshot Snap(
        int gameId, Book book, double line, int? american, PriceKind kind, ClvQuality quality, DateTimeOffset at)
        => new()
        {
            GameId = gameId,
            Book = book,
            Market = "TOTAL",
            Side = "over",
            Line = line,
            AmericanOdds = american ?? -110,
            PriceKind = kind,
            ClvQuality = quality,
            AssumedJuice = american is null,
            CapturedAt = at,
        };

    static Book MapBook(string provider)
    {
        if (provider.Contains("Draft", StringComparison.OrdinalIgnoreCase)) return Book.DraftKings;
        if (provider.Contains("ESPN BET", StringComparison.OrdinalIgnoreCase)) return Book.DraftKings;
        if (provider.Contains("FanDuel", StringComparison.OrdinalIgnoreCase)) return Book.FanDuel;
        return Book.DraftKings; // ESPN site odds default to DK widget
    }

    /// <summary>Parse open/close totals from a core-odds detail or pickcenter object.</summary>
    public static ParsedTotal? ParseOddsDetail(JsonElement root)
    {
        var provider = root.TryGetProperty("provider", out var p) && p.TryGetProperty("name", out var n)
            ? n.GetString() ?? ""
            : "";

        double? openLine = null, closeLine = null;
        int? openAm = null, closeAm = null;

        if (root.TryGetProperty("open", out var openEl) && openEl.ValueKind == JsonValueKind.Object)
        {
            openLine = ReadTotalLine(openEl);
            openAm = ReadAmericanUnder(openEl, "over");
        }

        if (root.TryGetProperty("close", out var closeEl) && closeEl.ValueKind == JsonValueKind.Object)
        {
            closeLine = ReadTotalLine(closeEl);
            closeAm = ReadAmericanUnder(closeEl, "over");
        }

        // pickcenter nested total.over.open / .close
        if (root.TryGetProperty("total", out var tot) && tot.ValueKind == JsonValueKind.Object
            && tot.TryGetProperty("over", out var over) && over.ValueKind == JsonValueKind.Object)
        {
            if (over.TryGetProperty("open", out var po) && po.ValueKind == JsonValueKind.Object)
            {
                openLine ??= ReadLineToken(po, "line");
                openAm ??= ReadAmericanField(po, "odds");
            }

            if (over.TryGetProperty("close", out var pc) && pc.ValueKind == JsonValueKind.Object)
            {
                closeLine ??= ReadLineToken(pc, "line");
                closeAm ??= ReadAmericanField(pc, "odds");
            }
        }

        if (closeLine is null && root.TryGetProperty("overUnder", out var ou))
        {
            if (ou.ValueKind == JsonValueKind.Number && ou.TryGetDouble(out var v))
                closeLine = v;
            else if (ou.ValueKind == JsonValueKind.String
                     && double.TryParse(ou.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var vs))
                closeLine = vs;
            closeAm ??= ReadAmericanField(root, "overOdds");
        }

        if (openLine is null && closeLine is null) return null;
        return new ParsedTotal(openLine, openAm, closeLine, closeAm, provider);
    }

    static double? ReadTotalLine(JsonElement blob)
    {
        if (!blob.TryGetProperty("total", out var t) || t.ValueKind != JsonValueKind.Object)
            return null;
        foreach (var key in new[] { "alternateDisplayValue", "american", "displayValue" })
        {
            if (!t.TryGetProperty(key, out var el)) continue;
            if (el.ValueKind == JsonValueKind.Number && el.TryGetDouble(out var n)) return n;
            if (el.ValueKind == JsonValueKind.String
                && double.TryParse(el.GetString()?.TrimStart('o', 'O', 'u', 'U'), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var s))
                return s;
        }

        return null;
    }

    static double? ReadLineToken(JsonElement blob, string field)
    {
        if (!blob.TryGetProperty(field, out var el)) return null;
        var raw = el.ValueKind == JsonValueKind.String ? el.GetString()
            : el.ValueKind == JsonValueKind.Number && el.TryGetDouble(out var n) ? n.ToString(CultureInfo.InvariantCulture)
            : null;
        if (raw is null) return null;
        var m = LineToken.Match(raw);
        return m.Success
            && double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
            ? v
            : null;
    }

    static int? ReadAmericanUnder(JsonElement blob, string side)
    {
        if (!blob.TryGetProperty(side, out var s) || s.ValueKind != JsonValueKind.Object) return null;
        return ReadAmericanField(s, "american") ?? ReadAmericanField(s, "alternateDisplayValue");
    }

    static int? ReadAmericanField(JsonElement blob, string name)
    {
        if (!blob.TryGetProperty(name, out var el)) return null;
        if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var i)) return i;
        if (el.ValueKind == JsonValueKind.Number && el.TryGetDouble(out var d)) return (int)Math.Round(d);
        if (el.ValueKind == JsonValueKind.String
            && int.TryParse(el.GetString()?.Replace("+", ""), NumberStyles.Integer, CultureInfo.InvariantCulture,
                out var s))
            return s;
        return null;
    }

    static async Task<ParsedTotal?> FetchParsedTotalAsync(string sport, string espnId, CancellationToken ct)
    {
        if (!CorePath.TryGetValue(sport, out var core)) return null;

        // Prefer core odds (has open/close blocks).
        try
        {
            var listUrl =
                $"https://sports.core.api.espn.com/v2/sports/{core.Sport}/leagues/{core.League}/events/{espnId}/competitions/{espnId}/odds";
            using var listDoc = await Http.GetFromJsonAsync<JsonDocument>(listUrl, ct);
            if (listDoc is not null && listDoc.RootElement.TryGetProperty("items", out var items)
                                     && items.ValueKind == JsonValueKind.Array)
            {
                ParsedTotal? any = null;
                foreach (var it in items.EnumerateArray())
                {
                    JsonElement detail;
                    JsonDocument? owned = null;
                    try
                    {
                        if (it.TryGetProperty("$ref", out var pref))
                        {
                            var refUrl = pref.GetString()?.Replace("http://", "https://");
                            if (refUrl is null) continue;
                            owned = await Http.GetFromJsonAsync<JsonDocument>(refUrl, ct);
                            if (owned is null) continue;
                            detail = owned.RootElement;
                        }
                        else
                        {
                            detail = it;
                        }

                        var parsed = ParseOddsDetail(detail);
                        if (parsed is null) continue;
                        var prov = parsed.Value.Provider;
                        if (prov.Contains("Draft", StringComparison.OrdinalIgnoreCase)
                            || prov.Contains("ESPN BET", StringComparison.OrdinalIgnoreCase))
                            return parsed;
                        any ??= parsed;
                    }
                    finally
                    {
                        owned?.Dispose();
                    }
                }

                if (any is not null) return any;
            }
        }
        catch
        {
            // fall through to summary pickcenter
        }

        if (!SitePath.TryGetValue(sport, out var site)) return null;
        try
        {
            var summaryUrl =
                $"https://site.api.espn.com/apis/site/v2/sports/{site}/summary?event={espnId}";
            using var sum = await Http.GetFromJsonAsync<JsonDocument>(summaryUrl, ct);
            if (sum is null || !sum.RootElement.TryGetProperty("pickcenter", out var pc)
                            || pc.ValueKind != JsonValueKind.Array)
                return null;

            ParsedTotal? any = null;
            foreach (var item in pc.EnumerateArray())
            {
                var parsed = ParseOddsDetail(item);
                if (parsed is null) continue;
                var prov = parsed.Value.Provider;
                if (prov.Contains("Draft", StringComparison.OrdinalIgnoreCase)
                    || prov.Contains("ESPN BET", StringComparison.OrdinalIgnoreCase))
                    return parsed;
                any ??= parsed;
            }

            return any;
        }
        catch
        {
            return null;
        }
    }

    static async Task AttachEspnIdsAsync(
        AppDbContext db, List<Game> warehouse, DateOnly start, DateOnly end, CancellationToken ct)
    {
        var need = warehouse.Where(g => string.IsNullOrWhiteSpace(g.EspnGameId)).ToList();
        if (need.Count == 0) return;

        var sports = need.Select(g => g.Sport).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var bySport = warehouse.GroupBy(g => g.Sport, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        foreach (var sport in sports)
        {
            if (!SitePath.TryGetValue(sport, out var path)) continue;
            if (!bySport.TryGetValue(sport, out var games)) continue;

            for (var d = start; d <= end; d = d.AddDays(1))
            {
                try
                {
                    var url =
                        $"https://site.api.espn.com/apis/site/v2/sports/{path}/scoreboard?dates={d:yyyyMMdd}";
                    using var doc = await Http.GetFromJsonAsync<JsonDocument>(url, ct);
                    if (doc is null) continue;
                    foreach (var ev in ParseScoreboardEvents(doc, sport))
                    {
                        var hit = LineTrackerImporter.FindGame(games, sport, ev.Home, ev.Away, ev.Kickoff);
                        if (hit is null || !string.IsNullOrWhiteSpace(hit.EspnGameId)) continue;
                        hit.EspnGameId = ev.EspnId;
                    }
                }
                catch
                {
                    // keep going; miss counted later
                }
            }
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>US-Eastern calendar day for US leagues; UTC day for soccer (same as ResultsRefresh).</summary>
    public static DateOnly KeyDate(string sport, DateTimeOffset kickoff)
        => sport is "NBA" or "NHL" or "NFL" or "NCAAB" or "NCAAF"
            ? DateOnly.FromDateTime(
                TimeZoneInfo.ConvertTime(kickoff, TimeZoneInfo.FindSystemTimeZoneById("America/New_York")).DateTime)
            : DateOnly.FromDateTime(kickoff.UtcDateTime);

    readonly record struct SbEvent(string EspnId, DateTimeOffset Kickoff, string Home, string Away);

    static List<SbEvent> ParseScoreboardEvents(JsonDocument doc, string sport)
    {
        var list = new List<SbEvent>();
        if (!doc.RootElement.TryGetProperty("events", out var events)) return list;
        foreach (var e in events.EnumerateArray())
        {
            try
            {
                var id = e.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "";
                if (id.Length == 0) continue;
                if (!e.TryGetProperty("competitions", out var comps) || comps.GetArrayLength() == 0) continue;
                var comp = comps[0];
                if (!DateTimeOffset.TryParse(e.GetProperty("date").GetString(), CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var kickoff))
                    continue;
                string? home = null, away = null;
                foreach (var c in comp.GetProperty("competitors").EnumerateArray())
                {
                    var t = c.GetProperty("team");
                    string? S(string p) => t.TryGetProperty(p, out var v) ? v.GetString() : null;
                    var name = sport switch
                    {
                        "NBA" => TeamCodes.TryNormalize(S("abbreviation")) ?? S("abbreviation"),
                        "NFL" => SportTeams.TryNfl(S("abbreviation")) ?? S("abbreviation"),
                        "NHL" => SportTeams.ResolveNhl(S("abbreviation"), S("displayName"), S("shortDisplayName")),
                        "NCAAB" or "NCAAF" => SportTeams.ResolveNcaa(S("abbreviation"), S("displayName"),
                            S("shortDisplayName")),
                        _ => SportTeams.ResolveSoccer(sport, S("abbreviation"), S("displayName"),
                            S("shortDisplayName")),
                    };
                    if (name is null) { home = away = null; break; }
                    if (c.GetProperty("homeAway").GetString() == "home") home = name;
                    else away = name;
                }

                if (home is null || away is null) continue;
                list.Add(new SbEvent(id, kickoff, home, away));
            }
            catch
            {
                // skip malformed
            }
        }

        return list;
    }
}
