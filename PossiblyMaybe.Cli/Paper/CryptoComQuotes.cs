using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Cli.Paper;

/// <summary>
/// Crypto.com Predict game totals via public Market Data API (no key for personal use).
/// https://data-api.crypto.com/api/v1/predictions — YES buy price on Over/Under contracts.
/// </summary>
public static class CryptoComQuotes
{
    const string Base = "https://data-api.crypto.com/api/v1/predictions";
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(40) };

    static readonly Dictionary<string, string[]> NameHints = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ATL"] = ["Atlanta"], ["BOS"] = ["Boston"], ["BKN"] = ["Brooklyn"], ["CHA"] = ["Charlotte"],
        ["CHI"] = ["Chicago"], ["CLE"] = ["Cleveland"], ["DAL"] = ["Dallas"], ["DEN"] = ["Denver"],
        ["DET"] = ["Detroit"], ["GSW"] = ["Golden State"], ["HOU"] = ["Houston"], ["IND"] = ["Indiana"],
        ["LAC"] = ["Los Angeles C", "Clippers"], ["LAL"] = ["Los Angeles L", "Lakers"],
        ["MEM"] = ["Memphis"], ["MIA"] = ["Miami"], ["MIL"] = ["Milwaukee"], ["MIN"] = ["Minnesota"],
        ["NOP"] = ["New Orleans"], ["NYK"] = ["New York"], ["OKC"] = ["Oklahoma City"],
        ["ORL"] = ["Orlando"], ["PHI"] = ["Philadelphia"], ["PHX"] = ["Phoenix"], ["POR"] = ["Portland"],
        ["SAC"] = ["Sacramento"], ["SAS"] = ["San Antonio"], ["TOR"] = ["Toronto"], ["UTA"] = ["Utah"],
        ["WAS"] = ["Washington"],
        // MLS abbrevs used in paper keys — title search is best-effort
        ["SJ"] = ["San Jose", "Earthquakes"], ["SEA"] = ["Seattle"], ["COL"] = ["Colorado"],
        ["MTL"] = ["Montreal"], ["CLT"] = ["Charlotte"],
    };

    public readonly record struct Quote(
        string EventId,
        string Title,
        double Strike,
        double BuyPrice,
        int AmericanOdds,
        string SideLabel);

    public static async Task<Quote?> TryGetTotalAsync(
        string sport,
        string gameKey,
        string side,
        double line,
        CancellationToken ct = default)
    {
        if (!TryParseGameKey(gameKey, out _, out var away, out var home))
            return null;
        var kind = sport.ToUpperInvariant() switch
        {
            "NBA" => "NBA",
            "MLS" => "MLS",
            _ => null,
        };
        if (kind is null) return null;

        var ev = await FindMatchEventAsync(kind, away, home, ct);
        if (ev is null) return null;

        var contracts = await FetchContractsAsync(ev.Value.Id, ct);
        side = side.ToUpperInvariant();
        var wantOver = side == "OVER";
        if (side is not ("OVER" or "UNDER")) return null;

        var candidates = new List<(double Strike, double Buy, string Label)>();
        foreach (var c in contracts)
        {
            if (!c.TryGetProperty("market_type", out var mt) || mt.ValueKind != JsonValueKind.Object)
                continue;
            if (!mt.TryGetProperty("name", out var name) || name.GetString() != "total")
                continue;
            if (!mt.TryGetProperty("period", out var period) || period.GetString() != "full")
                continue;
            var title = c.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
            var m = Regex.Match(title, @"^(Over|Under)\s+(\d+(?:\.\d+)?)", RegexOptions.IgnoreCase);
            if (!m.Success) continue;
            var isOver = m.Groups[1].Value.Equals("Over", StringComparison.OrdinalIgnoreCase);
            if (isOver != wantOver) continue;
            if (!double.TryParse(m.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var strike))
                continue;
            var status = c.TryGetProperty("status", out var st) ? st.GetString() : null;
            if (status is "voided" or "suspended") continue;

            double? buy = null;
            if (wantOver)
                buy = ParsePrice(c, "yes");
            else
                buy = ParsePrice(c, "yes") ?? ParsePrice(c, "no"); // Under contract YES = under

            if (buy is null or <= 0 or >= 1) continue;
            candidates.Add((strike, buy.Value, wantOver ? "YES(over)" : "YES(under)"));
        }

        if (candidates.Count == 0) return null;
        var best = candidates
            .OrderBy(x => Math.Abs(x.Strike - line))
            .ThenBy(x => x.Strike)
            .First();

        return new Quote(
            ev.Value.Id,
            ev.Value.Title,
            best.Strike,
            best.Buy,
            OddsMath.AmericanFromBuyPrice(best.Buy),
            best.Label);
    }

    static double? ParsePrice(JsonElement c, string field)
    {
        if (!c.TryGetProperty(field, out var p)) return null;
        if (p.ValueKind == JsonValueKind.Number && p.TryGetDouble(out var d)) return d;
        if (p.ValueKind == JsonValueKind.String
            && double.TryParse(p.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var s)
            && s > 0)
            return s;
        return null;
    }

    static async Task<(string Id, string Title)?> FindMatchEventAsync(
        string kind, string away, string home, CancellationToken ct)
    {
        var queries = new List<string>();
        if (NameHints.TryGetValue(home, out var hh)) queries.AddRange(hh);
        if (NameHints.TryGetValue(away, out var aa)) queries.AddRange(aa);
        queries.Add(home);
        queries.Add(away);

        foreach (var q in queries.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var url = $"{Base}/events/search?q={Uri.EscapeDataString(q)}&limit=20";
            using var doc = await Http.GetFromJsonAsync<JsonDocument>(url, ct);
            if (doc is null) continue;
            if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
                continue;
            foreach (var e in data.EnumerateArray())
            {
                if (e.TryGetProperty("kind", out var k) && k.GetString() != kind) continue;
                if (e.TryGetProperty("type", out var ty) && ty.GetString() != "match") continue;
                var title = e.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
                if (!TitleHasTeam(title, away) || !TitleHasTeam(title, home)) continue;
                var id = e.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                if (string.IsNullOrEmpty(id)) continue;
                return (id, title);
            }
        }

        return null;
    }

    static bool TitleHasTeam(string title, string abbr)
    {
        if (NameHints.TryGetValue(abbr, out var hints))
            return hints.Any(h => title.Contains(h, StringComparison.OrdinalIgnoreCase));
        return title.Contains(abbr, StringComparison.OrdinalIgnoreCase);
    }

    static async Task<List<JsonElement>> FetchContractsAsync(string eventId, CancellationToken ct)
    {
        var url = $"{Base}/events/{eventId}/contracts?limit=500";
        using var doc = await Http.GetFromJsonAsync<JsonDocument>(url, ct);
        if (doc is null) return [];
        if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            return [];
        return data.EnumerateArray().Select(e => e.Clone()).ToList();
    }

    static bool TryParseGameKey(string key, out string ymd, out string away, out string home)
    {
        ymd = away = home = "";
        var m = Regex.Match(key.Trim(), @"^(\d{8})\s+(\w+)@(\w+)$");
        if (!m.Success) return false;
        ymd = m.Groups[1].Value;
        away = m.Groups[2].Value.ToUpperInvariant();
        home = m.Groups[3].Value.ToUpperInvariant();
        return true;
    }
}
