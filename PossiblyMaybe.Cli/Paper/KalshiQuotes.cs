using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Cli.Paper;

/// <summary>Local Kalshi public market quotes for NBA/MLS game totals (YES = over strike).</summary>
public static class KalshiQuotes
{
    const string Base = "https://external-api.kalshi.com/trade-api/v2";
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };
    static readonly Dictionary<string, string> Series = new(StringComparer.OrdinalIgnoreCase)
    {
        ["NBA"] = "KXNBATOTAL",
        ["MLS"] = "KXMLSTOTAL",
    };

    public readonly record struct Quote(
        string EventTicker,
        string MarketTicker,
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
        if (!Series.TryGetValue(sport, out var series))
            return null;
        if (!TryParseGameKey(gameKey, out var ymd, out var away, out var home))
            return null;

        var markets = await FetchOpenMarketsAsync(series, ct);
        var stamp = ToKalshiDate(ymd);
        var eventMarkets = markets
            .Where(m =>
            {
                var et = m.EventTicker;
                if (!et.Contains(stamp, StringComparison.OrdinalIgnoreCase))
                    return false;
                var tail = et.Contains('-') ? et[(et.LastIndexOf('-') + 1)..] : et;
                tail = tail.ToUpperInvariant();
                return tail.Contains(away + home, StringComparison.Ordinal)
                       || tail.Contains(home + away, StringComparison.Ordinal)
                       || (tail.Contains(away, StringComparison.Ordinal) && tail.Contains(home, StringComparison.Ordinal));
            })
            .ToList();
        if (eventMarkets.Count == 0)
            return null;

        var mkt = eventMarkets
            .Select(m => (m, dist: Math.Abs((m.FloorStrike ?? double.NaN) - line)))
            .Where(t => !double.IsNaN(t.dist))
            .OrderBy(t => t.dist)
            .ThenBy(t => t.m.FloorStrike)
            .Select(t => t.m)
            .FirstOrDefault();
        if (mkt is null || mkt.FloorStrike is null)
            return null;

        side = side.ToUpperInvariant();
        double? buy;
        string label;
        if (side == "OVER")
        {
            buy = mkt.YesAsk;
            label = "YES(over)";
        }
        else if (side == "UNDER")
        {
            buy = mkt.NoAsk ?? (mkt.YesBid is { } yb ? 1 - yb : null);
            label = "NO(under)";
        }
        else
            return null;

        if (buy is null or <= 0 or >= 1)
            return null;

        return new Quote(
            mkt.EventTicker,
            mkt.Ticker,
            mkt.FloorStrike.Value,
            buy.Value,
            OddsMath.AmericanFromBuyPrice(buy.Value),
            label);
    }

    static bool TryParseGameKey(string key, out string ymd, out string away, out string home)
    {
        ymd = away = home = "";
        var m = System.Text.RegularExpressions.Regex.Match(
            key.Trim(), @"^(\d{8})\s+(\w+)@(\w+)$");
        if (!m.Success) return false;
        ymd = m.Groups[1].Value;
        away = m.Groups[2].Value.ToUpperInvariant();
        home = m.Groups[3].Value.ToUpperInvariant();
        return true;
    }

    static string ToKalshiDate(string yyyymmdd)
        => DateTime.ParseExact(yyyymmdd, "yyyyMMdd", CultureInfo.InvariantCulture)
            .ToString("yyMMMdd", CultureInfo.InvariantCulture).ToUpperInvariant();

    static async Task<List<Mkt>> FetchOpenMarketsAsync(string series, CancellationToken ct)
    {
        var list = new List<Mkt>();
        string? cursor = null;
        do
        {
            var path = $"/markets?series_ticker={series}&status=open&limit=200"
                       + (cursor is null ? "" : $"&cursor={Uri.EscapeDataString(cursor)}");
            using var doc = await Http.GetFromJsonAsync<JsonDocument>(Base + path, ct)
                           ?? throw new InvalidOperationException("Kalshi markets empty");
            var root = doc.RootElement;
            if (root.TryGetProperty("markets", out var arr))
            {
                foreach (var el in arr.EnumerateArray())
                    list.Add(Parse(el));
            }
            cursor = root.TryGetProperty("cursor", out var c) && c.ValueKind == JsonValueKind.String
                ? c.GetString()
                : null;
        } while (!string.IsNullOrEmpty(cursor));

        return list;
    }

    static Mkt Parse(JsonElement el)
    {
        static double? D(JsonElement e, string name)
        {
            if (!e.TryGetProperty(name, out var p)) return null;
            if (p.ValueKind == JsonValueKind.Number && p.TryGetDouble(out var n)) return n;
            if (p.ValueKind == JsonValueKind.String && double.TryParse(p.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var s))
                return s;
            return null;
        }

        return new Mkt(
            el.GetProperty("ticker").GetString() ?? "",
            el.TryGetProperty("event_ticker", out var et) ? et.GetString() ?? "" : "",
            D(el, "floor_strike"),
            D(el, "yes_ask_dollars"),
            D(el, "yes_bid_dollars"),
            D(el, "no_ask_dollars"));
    }

    sealed record Mkt(
        string Ticker,
        string EventTicker,
        double? FloorStrike,
        double? YesAsk,
        double? YesBid,
        double? NoAsk);
}
