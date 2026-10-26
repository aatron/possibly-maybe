using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PossiblyMaybe.Data;
using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Cli.Paper;

/// <summary>
/// Live soft-spot slate: rebuild PaceEff (NBA) / xG EMA (MLS) from warehouse, pull ESPN totals, advise.
/// </summary>
public static class DailySlate
{
    private static readonly HttpClient Http = new()
    {
        DefaultRequestHeaders = { { "User-Agent", "PossiblyMaybe/1.0" } },
        Timeout = TimeSpan.FromSeconds(30),
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
            var line = g.Total ?? await FetchNbaTotalAsync(g.EspnId, ct);
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
            Console.WriteLine($"    paper-bet \"{key}\" {side} {line.Value} {juice} {advice.StakeFraction * bankroll:F2} {p:F3}");
        }

        Console.WriteLine(gos == 0
            ? "No NBA soft spots today — sit out. That is a win for bankroll discipline."
            : $"NBA GO tickets: {gos}");
    }

    public static async Task RunMlsAsync(string dbPath, string yyyymmdd, double bankroll, CancellationToken ct = default)
    {
        var (xgBook, goalsBook, seenXg) = await BuildMlsBooksAsync(dbPath, ct);
        var games = await FetchMlsScoreboardAsync(yyyymmdd, ct);
        Console.WriteLine($"######## MLS SLATE {yyyymmdd} bankroll=${bankroll:F0} paper xG ########");
        Console.WriteLine($"Gate: w={MlsChampConfig.MarketWeight} |exp−line|≥{MlsChampConfig.MinAbsTotalEdge} EV≥{MlsChampConfig.MinEv:P0}");
        if (games.Count == 0)
        {
            Console.WriteLine("No MLS games on this date.");
            return;
        }

        var gos = 0;
        foreach (var g in games)
        {
            var useXg = seenXg.Contains(g.Home) && seenXg.Contains(g.Away);
            var modelExp = useXg
                ? xgBook.ExpectedTotal(g.Home, g.Away)
                : goalsBook.ExpectedTotal(g.Home, g.Away);
            var line = g.Total ?? await FetchMlsTotalAsync(g.EspnId, ct);
            if (line is null)
            {
                Console.WriteLine($"SKIP {g.Away}@{g.Home} — no total");
                continue;
            }

            var exp = MlsChampConfig.MarketWeight * line.Value + (1 - MlsChampConfig.MarketWeight) * modelExp;
            var edge = exp - line.Value;
            var juice = -110;
            if (Math.Abs(edge) < MlsChampConfig.MinAbsTotalEdge)
            {
                Console.WriteLine($"NO-GO {g.Away}@{g.Home}  model={modelExp:F2} line={line:F1} edge={edge:+0.2;-0.2}");
                continue;
            }

            var betOver = edge > 0;
            var p = betOver
                ? TotalsMath.OverProb(exp, line.Value, sharpness: 1.2)
                : 1 - TotalsMath.OverProb(exp, line.Value, sharpness: 1.2);
            var advice = BetDecider.Advise(p, juice, bankroll, MlsChampConfig.MinEv);
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
            Console.WriteLine($"    paper-bet \"{key}\" {side} {line.Value} {juice} {advice.StakeFraction * bankroll:F2} {p:F3}");
        }

        Console.WriteLine(gos == 0
            ? "No MLS soft spots today — sit out."
            : $"MLS GO tickets: {gos}");
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

    private static async Task<(ScoringBook Xg, ScoringBook Goals, HashSet<string> SeenXg)> BuildMlsBooksAsync(
        string dbPath, CancellationToken ct)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={dbPath}").Options;
        await using var db = new AppDbContext(options);
        var games = (await db.Games.AsNoTracking()
                .Where(g => g.Sport == "MLS" && g.HomeScore != null)
                .ToListAsync(ct))
            .OrderBy(g => g.TipoffUtc).ThenBy(g => g.Id)
            .ToList();
        Dictionary<int, MlsGameXg> xg = [];
        try { xg = await db.MlsGameXgs.AsNoTracking().ToDictionaryAsync(x => x.GameId, ct); }
        catch { /* */ }

        var xgBook = new ScoringBook(alpha: 0.18, leagueInit: 2.7);
        var goalsBook = new ScoringBook(alpha: 0.18, leagueInit: 2.7);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var g in games)
        {
            goalsBook.Update(g.HomeTeam, g.AwayTeam, g.HomeScore!.Value, g.AwayScore!.Value);
            if (!xg.TryGetValue(g.Id, out var row)) continue;
            xgBook.Update(g.HomeTeam, g.AwayTeam, row.HomeXg, row.AwayXg);
            seen.Add(g.HomeTeam);
            seen.Add(g.AwayTeam);
        }
        return (xgBook, goalsBook, seen);
    }

    private static async Task<List<SlateGame>> FetchNbaScoreboardAsync(string yyyymmdd, CancellationToken ct)
    {
        var url = $"https://site.api.espn.com/apis/site/v2/sports/basketball/nba/scoreboard?dates={yyyymmdd}";
        using var doc = await Http.GetFromJsonAsync<JsonDocument>(url, ct)
            ?? throw new InvalidOperationException("NBA scoreboard empty");
        return ParseScoreboard(doc, nbaAbbrev: true);
    }

    private static async Task<List<SlateGame>> FetchMlsScoreboardAsync(string yyyymmdd, CancellationToken ct)
    {
        var url = $"https://site.api.espn.com/apis/site/v2/sports/soccer/usa.1/scoreboard?dates={yyyymmdd}";
        using var doc = await Http.GetFromJsonAsync<JsonDocument>(url, ct)
            ?? throw new InvalidOperationException("MLS scoreboard empty");
        return ParseScoreboard(doc, nbaAbbrev: false);
    }

    private static List<SlateGame> ParseScoreboard(JsonDocument doc, bool nbaAbbrev)
    {
        var list = new List<SlateGame>();
        if (!doc.RootElement.TryGetProperty("events", out var events)) return list;
        foreach (var e in events.EnumerateArray())
        {
            var id = e.GetProperty("id").GetString() ?? "";
            var comps = e.GetProperty("competitions")[0];
            string? home = null, away = null;
            foreach (var c in comps.GetProperty("competitors").EnumerateArray())
            {
                var team = c.GetProperty("team");
                string? name;
                if (nbaAbbrev)
                {
                    name = team.TryGetProperty("abbreviation", out var ab) ? ab.GetString() : null;
                    name = TeamCodes.TryNormalize(name) ?? name;
                }
                else
                {
                    // Prefer abbrev → displayName through MLS map (DB uses short codes)
                    var abbr = team.TryGetProperty("abbreviation", out var ab) ? ab.GetString() : null;
                    var display = team.TryGetProperty("displayName", out var dn) ? dn.GetString() : null;
                    name = SportTeams.TryMls(abbr) ?? SportTeams.TryMls(display)
                           ?? SportTeams.TryMls(team.TryGetProperty("shortDisplayName", out var sd) ? sd.GetString() : null);
                }
                if (c.GetProperty("homeAway").GetString() == "home") home = name;
                else away = name;
            }
            if (home is null || away is null) continue;
            // Prefer DraftKings-style total from scoreboard odds when present
            double? total = null;
            if (comps.TryGetProperty("odds", out var odds) && odds.ValueKind == JsonValueKind.Array)
            {
                foreach (var o in odds.EnumerateArray())
                {
                    if (o.TryGetProperty("overUnder", out var ou) && ou.TryGetDouble(out var v))
                    {
                        total = v;
                        break;
                    }
                }
            }
            list.Add(new SlateGame(id, home, away, total));
        }
        return list;
    }

    private static async Task<double?> FetchNbaTotalAsync(string espnId, CancellationToken ct)
        => await FetchEspnTotalAsync("basketball", "nba", espnId, ct);

    private static async Task<double?> FetchMlsTotalAsync(string espnId, CancellationToken ct)
        => await FetchEspnTotalAsync("soccer", "usa.1", espnId, ct);

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
