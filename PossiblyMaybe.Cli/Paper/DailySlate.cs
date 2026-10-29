using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PossiblyMaybe.Data;
using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Cli.Paper;

/// <summary>
/// Live soft-spot slate: NBA PaceEff + HOLD-PASS soccer (MLS / Ligue1 / LigaMX / SerieA).
/// </summary>
public static class DailySlate
{
    private static readonly HttpClient Http = new()
    {
        DefaultRequestHeaders = { { "User-Agent", "PossiblyMaybe/1.0" } },
        Timeout = TimeSpan.FromSeconds(30),
    };

    /// <summary>ESPN soccer league path for HOLD-PASS paper sports.</summary>
    public static readonly IReadOnlyDictionary<string, string> SoccerEspnPaths =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["MLS"] = "usa.1",
            ["LIGUE1"] = "fra.1",
            ["LIGAMX"] = "mex.1",
            ["SERIEA"] = "ita.1",
        };

    public static async Task RunNbaAsync(string dbPath, string yyyymmdd, double bankroll, CancellationToken ct = default)
    {
        var pace = await BuildNbaPaceAsync(dbPath, ct);
        var games = await FetchNbaScoreboardAsync(yyyymmdd, ct);
        Console.WriteLine($"######## NBA SLATE {yyyymmdd} bankroll=${bankroll:F0} champ PaceEff ########");
        Console.WriteLine($"Gate: w={ChampConfig.MarketWeight} |exp−line|≥{ChampConfig.MinAbsTotalEdge} EV≥{ChampConfig.MinEv:P0} ¼Kelly≤2%");
        var gos = 0;
        foreach (var g in games)
        {
            var modelExp = pace.ExpectedTotal(g.Home, g.Away);
            var line = g.Total ?? await FetchEspnTotalAsync("basketball", "nba", g.EspnId, ct);
            if (line is null)
            {
                Console.WriteLine($"SKIP {g.Away}@{g.Home} — no total");
                continue;
            }

            var exp = ChampConfig.MarketWeight * line.Value + (1 - ChampConfig.MarketWeight) * modelExp;
            var edge = exp - line.Value;
            var juice = -110;
            if (Math.Abs(edge) < ChampConfig.MinAbsTotalEdge)
            {
                Console.WriteLine($"NO-GO {g.Away}@{g.Home}  model={modelExp:F1} line={line:F1} edge={edge:F1}");
                continue;
            }

            var betOver = edge > 0;
            var p = betOver ? TotalsMath.OverProb(exp, line.Value) : 1 - TotalsMath.OverProb(exp, line.Value);
            var advice = BetDecider.Advise(p, juice, bankroll, ChampConfig.MinEv);
            var side = betOver ? "OVER" : "UNDER";
            if (!advice.Go)
            {
                Console.WriteLine($"NO-GO {g.Away}@{g.Home}  {side} {line:F1} edge={edge:F1} ({advice.Reason})");
                continue;
            }

            gos++;
            var key = $"{yyyymmdd} {g.Away}@{g.Home}";
            Console.WriteLine(
                $"GO  {key}  {side} {line:F1} @{juice}  model={modelExp:F1} edge={edge:F1} EV={advice.Ev:P1} stake=${advice.StakeFraction * bankroll:F2}");
            Console.WriteLine($"    paper-go \"{key}\" {side} {line.Value} {juice} {p:F3} {Math.Abs(edge):F2} NBA");
        }

        Console.WriteLine(gos == 0
            ? "No NBA soft spots today — sit out. That is a win for bankroll discipline."
            : $"NBA GO tickets: {gos}");
    }

    public static Task RunMlsAsync(string dbPath, string yyyymmdd, double bankroll, CancellationToken ct = default)
        => RunSoccerAsync("MLS", dbPath, yyyymmdd, bankroll, ct);

    /// <summary>HOLD-PASS soccer totals: MLS, LIGUE1, LIGAMX, SERIEA.</summary>
    public static async Task RunSoccerAsync(
        string sport, string dbPath, string yyyymmdd, double bankroll, CancellationToken ct = default)
    {
        sport = sport.ToUpperInvariant();
        if (!SoccerEspnPaths.TryGetValue(sport, out var espnLeague))
            throw new ArgumentException($"No live slate for {sport}. PASS paper: MLS, LIGUE1, LIGAMX, SERIEA.");

        var (modelName, w, minEdge, minEv, model) = await BuildSoccerModelAsync(sport, dbPath, ct);
        List<SlateGame> games;
        try
        {
            games = await FetchSoccerScoreboardAsync(sport, espnLeague, yyyymmdd, ct);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"######## {sport} SLATE {yyyymmdd} ########");
            Console.WriteLine($"Scoreboard fetch failed: {ex.Message}");
            return;
        }

        Console.WriteLine($"######## {sport} SLATE {yyyymmdd} bankroll=${bankroll:F0} paper {modelName} ########");
        Console.WriteLine($"Gate: w={w} |exp−line|≥{minEdge} EV≥{minEv:P0}");
        if (games.Count == 0)
        {
            Console.WriteLine($"No {sport} games on this date.");
            return;
        }

        var gos = 0;
        foreach (var g in games)
        {
            if (g.Home.StartsWith("?", StringComparison.Ordinal) || g.Away.StartsWith("?", StringComparison.Ordinal))
            {
                Console.WriteLine($"SKIP {g.Away}@{g.Home} — unmapped ESPN team");
                continue;
            }

            var modelExp = model(g.Home, g.Away);
            var line = g.Total ?? await FetchEspnTotalAsync("soccer", espnLeague, g.EspnId, ct);
            if (line is null)
            {
                Console.WriteLine($"SKIP {g.Away}@{g.Home} — no total");
                continue;
            }

            var exp = w * line.Value + (1 - w) * modelExp;
            var edge = exp - line.Value;
            var juice = -110;
            if (Math.Abs(edge) < minEdge)
            {
                Console.WriteLine($"NO-GO {g.Away}@{g.Home}  model={modelExp:F2} line={line:F1} edge={edge:+0.2;-0.2}");
                continue;
            }

            var betOver = edge > 0;
            var p = betOver
                ? TotalsMath.OverProb(exp, line.Value, sharpness: 1.2)
                : 1 - TotalsMath.OverProb(exp, line.Value, sharpness: 1.2);
            var advice = BetDecider.Advise(p, juice, bankroll, minEv);
            var side = betOver ? "OVER" : "UNDER";
            if (!advice.Go)
            {
                Console.WriteLine($"NO-GO {g.Away}@{g.Home}  {side} {line:F1} ({advice.Reason})");
                continue;
            }

            gos++;
            var key = $"{yyyymmdd} {g.Away}@{g.Home}";
            Console.WriteLine(
                $"GO  {key}  {side} {line:F1} @{juice}  model={modelExp:F2} edge={edge:+0.2;-0.2} EV={advice.Ev:P1} stake=${advice.StakeFraction * bankroll:F2}");
            Console.WriteLine($"    paper-go \"{key}\" {side} {line.Value} {juice} {p:F3} {Math.Abs(edge):F2} {sport}");
        }

        Console.WriteLine(gos == 0
            ? $"No {sport} soft spots today — sit out."
            : $"{sport} GO tickets: {gos}");
    }

    private static async Task<PaceEfficiencyBook> BuildNbaPaceAsync(string dbPath, CancellationToken ct)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={dbPath}").Options;
        await using var db = new AppDbContext(options);
        var games = (await db.Games.AsNoTracking()
                .Where(g => g.Sport == "NBA" && g.HomeScore != null)
                .ToListAsync(ct))
            .OrderBy(g => g.TipoffUtc).ThenBy(g => g.Id)
            .ToList();
        Dictionary<int, GameBox> boxes = [];
        try { boxes = await db.GameBoxes.AsNoTracking().ToDictionaryAsync(b => b.GameId, ct); }
        catch { /* */ }

        var pace = new PaceEfficiencyBook();
        foreach (var g in games)
        {
            if (!boxes.TryGetValue(g.Id, out var b)) continue;
            pace.Update(g.HomeTeam, g.AwayTeam,
                b.FgaHome, b.OrebHome, b.TovHome, b.FtaHome, b.PtsHome, b.Fg3aHome,
                b.FgaAway, b.OrebAway, b.TovAway, b.FtaAway, b.PtsAway, b.Fg3aAway);
        }
        return pace;
    }

    private static async Task<(string ModelName, double W, double MinEdge, double MinEv, Func<string, string, double> Model)>
        BuildSoccerModelAsync(string sport, string dbPath, CancellationToken ct)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={dbPath}").Options;
        await using var db = new AppDbContext(options);
        var games = (await db.Games.AsNoTracking()
                .Where(g => g.Sport == sport && g.HomeScore != null)
                .ToListAsync(ct))
            .OrderBy(g => g.TipoffUtc).ThenBy(g => g.Id)
            .ToList();

        if (sport == "MLS")
        {
            Dictionary<int, MlsGameXg> xg = [];
            try { xg = await db.MlsGameXgs.AsNoTracking().ToDictionaryAsync(x => x.GameId, ct); }
            catch { /* */ }
            var xgBook = new ScoringBook(alpha: MlsChampConfig.EmaAlpha, leagueInit: 2.7);
            var goalsBook = new ScoringBook(alpha: MlsChampConfig.EmaAlpha, leagueInit: 2.7);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var g in games)
            {
                goalsBook.Update(g.HomeTeam, g.AwayTeam, g.HomeScore!.Value, g.AwayScore!.Value);
                if (!xg.TryGetValue(g.Id, out var row)) continue;
                xgBook.Update(g.HomeTeam, g.AwayTeam, row.HomeXg, row.AwayXg);
                seen.Add(g.HomeTeam);
                seen.Add(g.AwayTeam);
            }

            return (MlsChampConfig.Model, MlsChampConfig.MarketWeight, MlsChampConfig.MinAbsTotalEdge,
                MlsChampConfig.MinEv,
                (home, away) => seen.Contains(home) && seen.Contains(away)
                    ? xgBook.ExpectedTotal(home, away)
                    : goalsBook.ExpectedTotal(home, away));
        }

        if (sport == "LIGUE1")
        {
            var book = new ScoringBook(alpha: Ligue1ChampConfig.EmaAlpha, leagueInit: 2.7);
            foreach (var g in games)
                book.Update(g.HomeTeam, g.AwayTeam, g.HomeScore!.Value, g.AwayScore!.Value);
            return (Ligue1ChampConfig.Model, Ligue1ChampConfig.MarketWeight, Ligue1ChampConfig.MinAbsTotalEdge,
                Ligue1ChampConfig.MinEv, book.ExpectedTotal);
        }

        if (sport == "LIGAMX")
        {
            var book = new ScoringBook(alpha: LigaMxChampConfig.EmaAlpha, leagueInit: 2.7);
            foreach (var g in games)
                book.Update(g.HomeTeam, g.AwayTeam, g.HomeScore!.Value, g.AwayScore!.Value);
            return (LigaMxChampConfig.Model, LigaMxChampConfig.MarketWeight, LigaMxChampConfig.MinAbsTotalEdge,
                LigaMxChampConfig.MinEv, book.ExpectedTotal);
        }

        if (sport == "SERIEA")
        {
            // Multiplicative DC over many seasons can collapse Att/Def to the floor.
            // Live book: last 3 calendar seasons only (same α / gates as holdout champ).
            var cutoff = DateTimeOffset.UtcNow.AddYears(-3);
            var dc = new DixonColesBook(alpha: SerieAChampConfig.DcAlpha);
            var goals = new ScoringBook(alpha: SerieAChampConfig.DcAlpha, leagueInit: 2.7);
            foreach (var g in games)
            {
                if (g.TipoffUtc < cutoff) continue;
                dc.Update(g.HomeTeam, g.AwayTeam, g.HomeScore!.Value, g.AwayScore!.Value);
                goals.Update(g.HomeTeam, g.AwayTeam, g.HomeScore!.Value, g.AwayScore!.Value);
            }

            return (SerieAChampConfig.Model, SerieAChampConfig.MarketWeight, SerieAChampConfig.MinAbsTotalEdge,
                SerieAChampConfig.MinEv,
                (home, away) =>
                {
                    var t = dc.ExpectedTotal(home, away);
                    // Pathological collapse → goals EMA fallback (still same edge/EV gates).
                    return t is >= 1.5 and <= 5.0 ? t : goals.ExpectedTotal(home, away);
                });
        }

        throw new ArgumentException($"No champ model for {sport}");
    }

    private static async Task<List<SlateGame>> FetchNbaScoreboardAsync(string yyyymmdd, CancellationToken ct)
    {
        var url = $"https://site.api.espn.com/apis/site/v2/sports/basketball/nba/scoreboard?dates={yyyymmdd}";
        using var doc = await Http.GetFromJsonAsync<JsonDocument>(url, ct)
            ?? throw new InvalidOperationException("NBA scoreboard empty");
        return ParseScoreboard(doc, "NBA");
    }

    private static async Task<List<SlateGame>> FetchSoccerScoreboardAsync(
        string sport, string espnLeague, string yyyymmdd, CancellationToken ct)
    {
        var url = $"https://site.api.espn.com/apis/site/v2/sports/soccer/{espnLeague}/scoreboard?dates={yyyymmdd}";
        using var doc = await Http.GetFromJsonAsync<JsonDocument>(url, ct)
            ?? throw new InvalidOperationException($"{sport} scoreboard empty");
        return ParseScoreboard(doc, sport);
    }

    private static List<SlateGame> ParseScoreboard(JsonDocument doc, string sport)
    {
        var list = new List<SlateGame>();
        if (!doc.RootElement.TryGetProperty("events", out var events)) return list;
        foreach (var e in events.EnumerateArray())
        {
            try
            {
                var id = e.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "";
                if (!e.TryGetProperty("competitions", out var compsArr) || compsArr.ValueKind != JsonValueKind.Array
                    || compsArr.GetArrayLength() == 0)
                    continue;
                var comps = compsArr[0];
                if (comps.ValueKind != JsonValueKind.Object) continue;
                string? home = null, away = null;
                if (!comps.TryGetProperty("competitors", out var competitors)
                    || competitors.ValueKind != JsonValueKind.Array)
                    continue;
                foreach (var c in competitors.EnumerateArray())
                {
                    if (!c.TryGetProperty("team", out var team) || team.ValueKind != JsonValueKind.Object)
                        continue;
                    var abbr = team.TryGetProperty("abbreviation", out var ab) ? ab.GetString() : null;
                    var display = team.TryGetProperty("displayName", out var dn) ? dn.GetString() : null;
                    var shortName = team.TryGetProperty("shortDisplayName", out var sd) ? sd.GetString() : null;
                    var name = ResolveTeam(sport, abbr, display, shortName);
                    var ha = c.TryGetProperty("homeAway", out var haEl) ? haEl.GetString() : null;
                    if (ha == "home") home = name;
                    else if (ha == "away") away = name;
                }
                if (home is null || away is null) continue;
                double? total = null;
                if (comps.TryGetProperty("odds", out var odds) && odds.ValueKind == JsonValueKind.Array)
                {
                    foreach (var o in odds.EnumerateArray())
                    {
                        if (o.ValueKind != JsonValueKind.Object) continue;
                        if (o.TryGetProperty("overUnder", out var ou)
                            && ou.ValueKind is JsonValueKind.Number
                            && ou.TryGetDouble(out var v))
                        {
                            total = v;
                            break;
                        }
                    }
                }
                list.Add(new SlateGame(id, home, away, total));
            }
            catch
            {
                // skip malformed ESPN event
            }
        }
        return list;
    }

    private static string? ResolveTeam(string sport, string? abbr, string? display, string? shortName)
    {
        if (sport == "NBA")
        {
            var n = TeamCodes.TryNormalize(abbr) ?? abbr;
            return n;
        }

        return sport.ToUpperInvariant() switch
        {
            "MLS" => SportTeams.TryMls(abbr) ?? SportTeams.TryMls(display) ?? SportTeams.TryMls(shortName)
                     ?? (display is null ? null : "?" + display),
            "LIGUE1" => SportTeams.TryLigue1(abbr) ?? SportTeams.TryLigue1(display) ?? SportTeams.TryLigue1(shortName)
                        ?? (display is null ? null : "?" + display),
            "SERIEA" => SportTeams.TrySerieA(abbr) ?? SportTeams.TrySerieA(display) ?? SportTeams.TrySerieA(shortName)
                        ?? (display is null ? null : "?" + display),
            "LIGAMX" => SportTeams.TryLigaMx(abbr) ?? SportTeams.TryLigaMx(display) ?? SportTeams.TryLigaMx(shortName)
                        ?? (display is null ? null : "?" + display),
            _ => display,
        };
    }

    private static async Task<double?> FetchEspnTotalAsync(string sport, string league, string espnId, CancellationToken ct)
    {
        try
        {
            var url =
                $"https://sports.core.api.espn.com/v2/sports/{sport}/leagues/{league}/events/{espnId}/competitions/{espnId}/odds";
            using var doc = await Http.GetFromJsonAsync<JsonDocument>(url, ct);
            if (doc is null || !doc.RootElement.TryGetProperty("items", out var items)) return null;
            foreach (var it in items.EnumerateArray())
            {
                if (!it.TryGetProperty("$ref", out var pref)) continue;
                var refUrl = pref.GetString()?.Replace("http://", "https://");
                if (refUrl is null) continue;
                using var det = await Http.GetFromJsonAsync<JsonDocument>(refUrl, ct);
                if (det is null) continue;
                var root = det.RootElement;
                var prov = root.TryGetProperty("provider", out var p) && p.TryGetProperty("name", out var n)
                    ? n.GetString() ?? ""
                    : "";
                // Prefer DraftKings / ESPN BET for soft retail number
                if (prov.Contains("Draft", StringComparison.OrdinalIgnoreCase)
                    || prov.Contains("ESPN BET", StringComparison.OrdinalIgnoreCase)
                    || prov.Contains("FanDuel", StringComparison.OrdinalIgnoreCase))
                {
                    if (root.TryGetProperty("overUnder", out var ou) && ou.TryGetDouble(out var v))
                        return v;
                }
            }

            // any provider
            foreach (var it in items.EnumerateArray())
            {
                if (!it.TryGetProperty("$ref", out var pref)) continue;
                var refUrl = pref.GetString()?.Replace("http://", "https://");
                if (refUrl is null) continue;
                using var det = await Http.GetFromJsonAsync<JsonDocument>(refUrl, ct);
                if (det?.RootElement.TryGetProperty("overUnder", out var ou) == true && ou.TryGetDouble(out var v))
                    return v;
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private readonly record struct SlateGame(string EspnId, string Home, string Away, double? Total);
}
