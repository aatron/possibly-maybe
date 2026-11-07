using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PossiblyMaybe.Data;
using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Cli.Import;

/// <summary>
/// Daily incremental refresh of completed results for every paper league (NBA + MLS/LIGUE1/LIGAMX/SERIEA)
/// from the ESPN scoreboard, plus NBA box lines (ESPN summary) and MLS xG (ASA). Idempotent via SourceKey.
/// </summary>
public static class ResultsRefresh
{
    public static readonly string[] Leagues = ["NBA", "MLS", "LIGUE1", "LIGAMX", "SERIEA"];

    private static readonly HttpClient Http = new()
    {
        DefaultRequestHeaders = { { "User-Agent", "PossiblyMaybe/1.0" } },
        Timeout = TimeSpan.FromSeconds(30),
    };

    private static readonly Dictionary<string, string> EspnPath = new(StringComparer.OrdinalIgnoreCase)
    {
        ["NBA"] = "basketball/nba",
        ["MLS"] = "soccer/usa.1",
        ["LIGUE1"] = "soccer/fra.1",
        ["LIGAMX"] = "soccer/mex.1",
        ["SERIEA"] = "soccer/ita.1",
    };

    public sealed record FinalGame(string EspnId, DateTimeOffset Kickoff, string Home, string Away, int HomeScore, int AwayScore);

    /// <summary>Only completed (status.type.completed) events with numeric scores; unfinished games are never returned.</summary>
    public static List<FinalGame> ParseFinals(JsonDocument doc, string sport)
    {
        var list = new List<FinalGame>();
        if (!doc.RootElement.TryGetProperty("events", out var events)) return list;
        foreach (var e in events.EnumerateArray())
        {
            try
            {
                var comp = e.GetProperty("competitions")[0];
                if (!comp.GetProperty("status").GetProperty("type").GetProperty("completed").GetBoolean()) continue;
                if (!DateTimeOffset.TryParse(e.GetProperty("date").GetString(), CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var kickoff))
                    continue;
                string? home = null, away = null;
                int hs = 0, as_ = 0;
                foreach (var c in comp.GetProperty("competitors").EnumerateArray())
                {
                    var t = c.GetProperty("team");
                    string? S(string p) => t.TryGetProperty(p, out var v) ? v.GetString() : null;
                    var name = sport == "NBA"
                        ? TeamCodes.TryNormalize(S("abbreviation"))
                        : SportTeams.ResolveSoccer(sport, S("abbreviation"), S("displayName"), S("shortDisplayName"));
                    if (name is null || !int.TryParse(c.GetProperty("score").GetString(), out var score)) { home = away = null; break; }
                    if (c.GetProperty("homeAway").GetString() == "home") { home = name; hs = score; }
                    else { away = name; as_ = score; }
                }
                if (home is null || away is null) continue;
                if (sport != "NBA" && (home.Contains("All-Stars") || away.Contains("All-Stars"))) continue; // exhibition, not league
                list.Add(new FinalGame(e.GetProperty("id").GetString() ?? "", kickoff, home, away, hs, as_));
            }
            catch { /* skip malformed ESPN event */ }
        }
        return list;
    }

    /// <summary>Date part of the SourceKey: US-Eastern day for NBA (CSV convention), UTC day for soccer.</summary>
    public static DateOnly KeyDate(string sport, DateTimeOffset kickoff)
        => sport == "NBA"
            ? DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(kickoff, TimeZoneInfo.FindSystemTimeZoneById("America/New_York")).DateTime)
            : DateOnly.FromDateTime(kickoff.UtcDateTime);

    public static string SeasonFor(string sport, DateOnly d) => sport switch
    {
        "NBA" => d.Month >= 10 ? $"{d.Year + 1}" : $"{d.Year}",
        "MLS" => $"{d.Year}",
        _ => d.Month >= 7 ? $"{d.Year}-{d.Year + 1}" : $"{d.Year - 1}-{d.Year}",
    };

    public static async Task<DateTimeOffset?> LatestResultAsync(AppDbContext db, string sport, CancellationToken ct = default)
    {
        var s = await db.Database.SqlQuery<string>(
            $"SELECT MAX(TipoffUtc) AS Value FROM Games WHERE Sport = {sport} AND HomeScore IS NOT NULL").FirstOrDefaultAsync(ct);
        return s is null ? null : DateTimeOffset.Parse(s, CultureInfo.InvariantCulture);
    }

    public static async Task RunAsync(string dbPath, DateOnly through, CancellationToken ct = default)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={dbPath}").Options;
        await using var db = new AppDbContext(options);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "MlsGameXgs" (
              "Id" INTEGER NOT NULL CONSTRAINT "PK_MlsGameXgs" PRIMARY KEY AUTOINCREMENT,
              "GameId" INTEGER NOT NULL, "HomeXg" REAL NOT NULL, "AwayXg" REAL NOT NULL);
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_MlsGameXgs_GameId" ON "MlsGameXgs" ("GameId");
            """, ct);

        foreach (var sport in Leagues)
        {
            var added = 0;
            try
            {
                var latest = await LatestResultAsync(db, sport, ct);
                var start = latest is null ? through.AddDays(-30) : DateOnly.FromDateTime(latest.Value.UtcDateTime).AddDays(-2);
                var existing = (await db.Games.Where(g => g.Sport == sport).ToListAsync(ct))
                    .Where(g => DateOnly.FromDateTime(g.TipoffUtc.UtcDateTime) >= start.AddDays(-3))
                    .ToList();
                var byKey = existing.ToDictionary(g => g.SourceKey, StringComparer.Ordinal);
                var byEspn = existing.Where(g => g.EspnGameId != null).GroupBy(g => g.EspnGameId!).ToDictionary(x => x.Key, x => x.First());
                var newTeams = new HashSet<string>();
                var known = sport == "NBA" ? null : new HashSet<string>(await db.Games.Where(g => g.Sport == sport).Select(g => g.HomeTeam).Union(db.Games.Where(g => g.Sport == sport).Select(g => g.AwayTeam)).Distinct().ToListAsync(ct));

                var days = Enumerable.Range(0, Math.Max(0, through.DayNumber - start.DayNumber + 1)).Select(i => start.AddDays(i)).ToList();
                foreach (var batch in days.Chunk(8)) // 8 days in flight: ~700 requests on a cold start
                {
                    var docs = await Task.WhenAll(batch.Select(d => Http.GetFromJsonAsync<JsonDocument>(
                        $"https://site.api.espn.com/apis/site/v2/sports/{EspnPath[sport]}/scoreboard?dates={d:yyyyMMdd}", ct)));
                    foreach (var doc in docs)
                    {
                        if (doc is null) throw new InvalidOperationException("empty scoreboard");
                        using var _ = doc;
                        foreach (var f in ParseFinals(doc, sport))
                        {
                            var key = SportTeams.GameKey(sport, KeyDate(sport, f.Kickoff), f.Home, f.Away);
                            if (!byKey.TryGetValue(key, out var g) && !byEspn.TryGetValue(f.EspnId, out g))
                            {
                                g = new Game
                                {
                                    Sport = sport, Season = SeasonFor(sport, KeyDate(sport, f.Kickoff)), SourceKey = key,
                                    EspnGameId = f.EspnId, TipoffUtc = f.Kickoff, HomeTeam = f.Home, AwayTeam = f.Away,
                                    HomeScore = f.HomeScore, AwayScore = f.AwayScore,
                                };
                                db.Games.Add(g);
                                byKey[key] = g;
                                byEspn[f.EspnId] = g;
                                added++;
                                if (known is not null && known.Add(f.Home)) newTeams.Add(f.Home);
                                if (known is not null && known.Add(f.Away)) newTeams.Add(f.Away);
                            }
                            else
                            {
                                g.EspnGameId ??= f.EspnId;
                                g.HomeScore ??= f.HomeScore;
                                g.AwayScore ??= f.AwayScore;
                            }
                        }
                    }
                }

                await db.SaveChangesAsync(ct);
                var extra = sport switch
                {
                    "NBA" => $" boxes+{await FillNbaBoxesAsync(db, through, ct)}",
                    "MLS" => $" xG+{await FillMlsXgAsync(db, ct)}",
                    _ => "",
                };
                Console.WriteLine($"{sport}: games+{added}{extra}"
                    + (newTeams.Count > 0 ? $"  NEW TEAMS (default rating): {string.Join(", ", newTeams)}" : ""));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{sport}: REFRESH FAILED ({ex.Message}) — stale guard will block GOs if data is old");
            }
        }

        foreach (var sport in Leagues)
        {
            var latest = await LatestResultAsync(db, sport, ct);
            Console.WriteLine($"latest result in DB  {sport,-7} {(latest is null ? "NONE" : latest.Value.ToString("yyyy-MM-dd"))}");
        }
    }

    /// <summary>ESPN summary box line for recent NBA games that have an EspnGameId but no GameBox yet.</summary>
    private static async Task<int> FillNbaBoxesAsync(AppDbContext db, DateOnly through, CancellationToken ct)
    {
        var have = (await db.GameBoxes.Select(b => b.GameId).ToListAsync(ct)).ToHashSet();
        var todo = (await db.Games.Where(g => g.Sport == "NBA" && g.EspnGameId != null && g.HomeScore != null).ToListAsync(ct))
            .Where(g => !have.Contains(g.Id) && DateOnly.FromDateTime(g.TipoffUtc.UtcDateTime) >= through.AddDays(-30)).ToList(); // 30d retry window; never backfills history
        var n = 0;
        foreach (var g in todo)
        {
            try
            {
                using var doc = await Http.GetFromJsonAsync<JsonDocument>(
                    $"https://site.api.espn.com/apis/site/v2/sports/basketball/nba/summary?event={g.EspnGameId}", ct);
                var box = doc is null ? null : ParseNbaBox(doc, g.HomeScore!.Value, g.AwayScore!.Value);
                if (box is null) continue;
                box.GameId = g.Id;
                db.GameBoxes.Add(box);
                n++;
            }
            catch { /* retried next run */ }
        }
        await db.SaveChangesAsync(ct);
        return n;
    }

    public static GameBox? ParseNbaBox(JsonDocument doc, int homePts, int awayPts)
    {
        double[]? h = null, a = null;
        foreach (var t in doc.RootElement.GetProperty("boxscore").GetProperty("teams").EnumerateArray())
        {
            var s = t.GetProperty("statistics").EnumerateArray()
                .ToDictionary(x => x.GetProperty("name").GetString()!, x => x.GetProperty("displayValue").GetString()!);
            static double Att(string v) => double.Parse(v.Split('-')[1], CultureInfo.InvariantCulture);
            static double D(string v) => double.Parse(v, CultureInfo.InvariantCulture);
            var row = new[]
            {
                Att(s["fieldGoalsMade-fieldGoalsAttempted"]), D(s["offensiveRebounds"]), D(s["totalTurnovers"]),
                Att(s["freeThrowsMade-freeThrowsAttempted"]), Att(s["threePointFieldGoalsMade-threePointFieldGoalsAttempted"]),
            };
            if (t.GetProperty("homeAway").GetString() == "home") h = row; else a = row;
        }
        if (h is null || a is null) return null;
        return new GameBox
        {
            FgaHome = h[0], OrebHome = h[1], TovHome = h[2], FtaHome = h[3], Fg3aHome = h[4], PtsHome = homePts,
            FgaAway = a[0], OrebAway = a[1], TovAway = a[2], FtaAway = a[3], Fg3aAway = a[4], PtsAway = awayPts,
        };
    }

    /// <summary>ASA xG (public API) joined to MLS Games by UTC date + mapped team codes.</summary>
    private static async Task<int> FillMlsXgAsync(AppDbContext db, CancellationToken ct)
    {
        var have = (await db.MlsGameXgs.Select(x => x.GameId).ToListAsync(ct)).ToHashSet();
        var games = (await db.Games.Where(g => g.Sport == "MLS").ToListAsync(ct)).Where(g => !have.Contains(g.Id))
            .ToDictionary(g => g.SourceKey, g => g, StringComparer.Ordinal);
        if (games.Count == 0) return 0;
        const string api = "https://app.americansocceranalysis.com/api/v1/mls";
        using var teams = await Http.GetFromJsonAsync<JsonDocument>($"{api}/teams", ct);
        var names = teams!.RootElement.EnumerateArray().ToDictionary(
            t => t.GetProperty("team_id").GetString()!, t => SportTeams.TryMls(t.GetProperty("team_name").GetString()));
        var n = 0;
        foreach (var season in games.Values.Select(g => g.TipoffUtc.Year).Distinct())
        {
            using var xg = await Http.GetFromJsonAsync<JsonDocument>($"{api}/games/xgoals?season_name={season}", ct);
            foreach (var x in xg!.RootElement.EnumerateArray())
            {
                if (!names.TryGetValue(x.GetProperty("home_team_id").GetString()!, out var home) || home is null) continue;
                if (!names.TryGetValue(x.GetProperty("away_team_id").GetString()!, out var away) || away is null) continue;
                var ko = DateTimeOffset.Parse(x.GetProperty("date_time_utc").GetString()!.Replace(" UTC", "Z"), CultureInfo.InvariantCulture);
                if (!games.TryGetValue(SportTeams.GameKey("MLS", DateOnly.FromDateTime(ko.UtcDateTime), home, away), out var g)) continue;
                db.MlsGameXgs.Add(new MlsGameXg
                {
                    GameId = g.Id,
                    HomeXg = x.GetProperty("home_team_xgoals").GetDouble(),
                    AwayXg = x.GetProperty("away_team_xgoals").GetDouble(),
                });
                games.Remove(g.SourceKey);
                n++;
            }
        }
        await db.SaveChangesAsync(ct);
        return n;
    }
}
