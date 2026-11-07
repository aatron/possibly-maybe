using Microsoft.EntityFrameworkCore;
using PossiblyMaybe.Data;
using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Cli.Variants;

/// <summary>
/// Historical shadow fill so variants start with history.
/// How to run (repo root):
///   dotnet run -c Release --project PossiblyMaybe.Cli -- variants-backfill
///   # optional: variants-backfill [warehouse.db] [variants.db]
/// Covers every season in warehouse for r3 paper leagues (NBA + HOLD-PASS soccer).
/// Persists <see cref="CandidateEntry"/> rows first; variants are filters. Replay window
/// {ReplayStart}–{ReplayEnd} is included in the full walk-forward.
/// V0 should reproduce champ-style GO set on the same open-line / −110 gates (Normal-σ NBA).
/// </summary>
public static class VariantBackfill
{
    public static readonly string ReplayStart = "20261030";
    public static readonly string ReplayEnd = "20261106";

    /// <summary>r3 path + C6 shadow leagues — backfill every season present in the warehouse for these.</summary>
    public static readonly string[] R3Leagues =
        ["NBA", "MLS", "LIGUE1", "LIGAMX", "SERIEA", "LALIGA", "EFL", "EPL"];

    public static async Task RunAsync(
        string warehouseDbPath,
        string variantsDbPath,
        CancellationToken ct = default)
    {
        var (vdb, _) = await VariantLedger.OpenAsync(variantsDbPath, ct);
        await using (vdb)
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={warehouseDbPath}").Options;
            await using var wdb = new AppDbContext(options);

            var present = (await wdb.Games.AsNoTracking()
                    .Select(g => g.Sport).Distinct().ToListAsync(ct))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var sport in R3Leagues)
            {
                if (!present.Contains(sport))
                {
                    Console.WriteLine($"variants-backfill: {sport} — no games in warehouse, skip");
                    continue;
                }

                Console.WriteLine(sport == "NBA"
                    ? $"variants-backfill: NBA walk-forward all seasons (incl. replay {ReplayStart}–{ReplayEnd})…"
                    : $"variants-backfill: {sport} all seasons…");
                var n = sport == "NBA"
                    ? await BackfillNbaAsync(wdb, vdb, ct)
                    : await BackfillSoccerAsync(wdb, vdb, sport, ct);
                Console.WriteLine($"  {sport} candidate×variant writes≈{n}");
            }
        }

        var settled = await VariantSettle.SettleOpenAsync(variantsDbPath, warehouseDbPath, ct);
        Console.WriteLine($"  settled {settled} shadow picks");
    }

    private static async Task<int> BackfillNbaAsync(
        AppDbContext wdb, VariantDbContext vdb, CancellationToken ct)
    {
        var games = (await wdb.Games.AsNoTracking()
                .Where(g => g.Sport == "NBA" && g.HomeScore != null)
                .ToListAsync(ct))
            .OrderBy(g => g.TipoffUtc).ThenBy(g => g.Id)
            .ToList();
        Dictionary<int, GameBox> boxes = [];
        try { boxes = await wdb.GameBoxes.AsNoTracking().ToDictionaryAsync(b => b.GameId, ct); }
        catch { /* */ }
        var odds = await wdb.OddsSnapshots.AsNoTracking()
            .Where(o => o.Market == "TOTAL").ToListAsync(ct);
        var byGame = odds.GroupBy(o => o.GameId).ToDictionary(g => g.Key, g => g.ToList());

        var pace = new PaceEfficiencyBook();
        var resid = new ExpandingResidualSigma();
        var shutoffSignal = new Dictionary<string, List<double>>(StringComparer.OrdinalIgnoreCase);
        var n = 0;
        foreach (var g in games)
        {
            byGame.TryGetValue(g.Id, out var snaps);
            snaps ??= [];
            var open = snaps.FirstOrDefault(s => s.PriceKind == PriceKind.Open);
            var close = snaps.FirstOrDefault(s => s.PriceKind == PriceKind.Close);
            var modelExp = pace.ExpectedTotal(g.HomeTeam, g.AwayTeam);
            var day = g.TipoffUtc.UtcDateTime.ToString("yyyyMMdd");

            if (open?.Line is { } line)
            {
                var c = new SlateCandidate(
                    "NBA", day, g.HomeTeam, g.AwayTeam,
                    modelExp, null, null,
                    line, line, // open-line backfill: no intraday move until W3
                    open.AmericanOdds == 0 ? -110 : open.AmericanOdds,
                    open.AmericanOdds == 0 ? -110 : open.AmericanOdds,
                    resid.Sigma,
                    ChampConfig.MarketWeight, ChampConfig.MinAbsTotalEdge, ChampConfig.MinEv,
                    Stale: false);
                n += await WriteAllAsync(vdb, c, close?.Line ?? line, shutoffSignal, ct);

                var exp = ChampConfig.MarketWeight * line + (1 - ChampConfig.MarketWeight) * modelExp;
                if (g.HomeScore is { } hs && g.AwayScore is { } as_)
                    resid.Observe(hs + as_ - exp);
            }

            if (boxes.TryGetValue(g.Id, out var b))
            {
                pace.Update(g.HomeTeam, g.AwayTeam,
                    b.FgaHome, b.OrebHome, b.TovHome, b.FtaHome, b.PtsHome, b.Fg3aHome,
                    b.FgaAway, b.OrebAway, b.TovAway, b.FtaAway, b.PtsAway, b.Fg3aAway);
            }
        }
        return n;
    }

    private static async Task<int> BackfillSoccerAsync(
        AppDbContext wdb, VariantDbContext vdb, string sport, CancellationToken ct)
    {
        var (w, minEdge, minEv, alpha, useDc) = sport switch
        {
            "MLS" => (MlsChampConfig.MarketWeight, MlsChampConfig.MinAbsTotalEdge, MlsChampConfig.MinEv,
                MlsChampConfig.EmaAlpha, false),
            "LIGUE1" => (Ligue1ChampConfig.MarketWeight, Ligue1ChampConfig.MinAbsTotalEdge, Ligue1ChampConfig.MinEv,
                Ligue1ChampConfig.EmaAlpha, false),
            "LIGAMX" => (LigaMxChampConfig.MarketWeight, LigaMxChampConfig.MinAbsTotalEdge, LigaMxChampConfig.MinEv,
                LigaMxChampConfig.EmaAlpha, false),
            "SERIEA" => (SerieAChampConfig.MarketWeight, SerieAChampConfig.MinAbsTotalEdge, SerieAChampConfig.MinEv,
                SerieAChampConfig.DcAlpha, true),
            "LALIGA" => (LaLigaShadowConfig.MarketWeight, LaLigaShadowConfig.MinAbsTotalEdge, LaLigaShadowConfig.MinEv,
                LaLigaShadowConfig.EmaAlpha, false),
            "EFL" => (EflShadowConfig.MarketWeight, EflShadowConfig.MinAbsTotalEdge, EflShadowConfig.MinEv,
                EflShadowConfig.EmaAlpha, false),
            "EPL" => (EplChampConfig.MarketWeight, EplChampConfig.MinAbsTotalEdge, EplChampConfig.MinEv,
                EplChampConfig.EmaAlpha, false),
            _ => throw new ArgumentException(sport),
        };

        var games = (await wdb.Games.AsNoTracking()
                .Where(g => g.Sport == sport && g.HomeScore != null)
                .ToListAsync(ct))
            .OrderBy(g => g.TipoffUtc).ThenBy(g => g.Id)
            .ToList();
        var odds = await wdb.OddsSnapshots.AsNoTracking()
            .Where(o => o.Market == "TOTAL").ToListAsync(ct);
        var byGame = odds.GroupBy(o => o.GameId).ToDictionary(g => g.Key, g => g.ToList());

        Dictionary<int, MlsGameXg>? xg = null;
        if (sport == "MLS")
        {
            try { xg = await wdb.MlsGameXgs.AsNoTracking().ToDictionaryAsync(x => x.GameId, ct); }
            catch { xg = null; }
        }

        var goals = new ScoringBook(alpha: alpha, leagueInit: 2.7);
        var xgBook = sport == "MLS" ? new ScoringBook(alpha: alpha, leagueInit: 2.7) : null;
        var xgSeen = new HashSet<string>(StringComparer.Ordinal);
        var dc = useDc ? new DixonColesBook(alpha: alpha) : null;
        var serieCutoff = DateTimeOffset.UtcNow.AddYears(-3);
        var shutoffSignal = new Dictionary<string, List<double>>(StringComparer.OrdinalIgnoreCase);
        var n = 0;

        foreach (var g in games)
        {
            byGame.TryGetValue(g.Id, out var snaps);
            snaps ??= [];
            var open = snaps.FirstOrDefault(s => s.PriceKind == PriceKind.Open);
            var close = snaps.FirstOrDefault(s => s.PriceKind == PriceKind.Close);
            double lh, la;
            if (sport == "MLS" && xgBook is not null
                && xgSeen.Contains(g.HomeTeam) && xgSeen.Contains(g.AwayTeam))
                (lh, la) = xgBook.ExpectedLambdas(g.HomeTeam, g.AwayTeam);
            else if (dc is not null && g.TipoffUtc >= serieCutoff)
            {
                (lh, la) = dc.ExpectedLambdas(g.HomeTeam, g.AwayTeam);
                var t = lh + la;
                if (t is < 1.5 or > 5.0)
                    (lh, la) = goals.ExpectedLambdas(g.HomeTeam, g.AwayTeam);
            }
            else
                (lh, la) = goals.ExpectedLambdas(g.HomeTeam, g.AwayTeam);

            var modelExp = lh + la;
            var day = g.TipoffUtc.UtcDateTime.ToString("yyyyMMdd");
            if (open?.Line is { } line)
            {
                var c = new SlateCandidate(
                    sport, day, g.HomeTeam, g.AwayTeam,
                    modelExp, lh, la,
                    line, line,
                    -110, -110,
                    0, w, minEdge, minEv, Stale: false);
                n += await WriteAllAsync(vdb, c, close?.Line ?? line, shutoffSignal, ct);
            }

            goals.Update(g.HomeTeam, g.AwayTeam, g.HomeScore!.Value, g.AwayScore!.Value);
            if (dc is not null && g.TipoffUtc >= serieCutoff)
                dc.Update(g.HomeTeam, g.AwayTeam, g.HomeScore!.Value, g.AwayScore!.Value);
            if (xgBook is not null && xg is not null && xg.TryGetValue(g.Id, out var row))
            {
                xgBook.Update(g.HomeTeam, g.AwayTeam, row.HomeXg, row.AwayXg);
                xgSeen.Add(g.HomeTeam);
                xgSeen.Add(g.AwayTeam);
            }
        }
        return n;
    }

    private static async Task<int> WriteAllAsync(
        VariantDbContext vdb,
        SlateCandidate c,
        double closeLine,
        Dictionary<string, List<double>> shutoffSignal,
        CancellationToken ct)
    {
        var key = VariantRegistry.GameKey(c);
        shutoffSignal.TryGetValue(c.Sport, out var window);
        var paused = window is not null && Guards.LeaguePausedByRollingClv(window)
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase) { c.Sport }
            : null;

        await VariantLedger.UpsertCandidateAsync(vdb, c, ct);

        VariantDecision? signal = null;
        var n = 0;
        foreach (var v in VariantRegistry.All)
        {
            var d = VariantRegistry.Evaluate(v, c, v.LeagueShutoff ? paused : null);
            await VariantLedger.UpsertAsync(vdb, v.Id, c.Sport, key, d, c.OpenLine, c.Line, ct);
            if (v.Id.Equals(Guards.ShutoffSignalVariantId, StringComparison.OrdinalIgnoreCase))
                signal = d;
            n++;
        }

        // Advance V3 CLV signal even when V4 is paused (so resume is possible).
        if (signal is { Pick: true, Side: not null, Line: not null })
        {
            var overClv = TotalsMath.OverClvPoints(signal.Line.Value, closeLine);
            var clv = signal.Side.Equals("OVER", StringComparison.OrdinalIgnoreCase) ? overClv : -overClv;
            if (window is null)
            {
                window = [];
                shutoffSignal[c.Sport] = window;
            }
            window.Add(clv);
            if (window.Count > Guards.ShutoffWindow)
                window.RemoveAt(0);
        }

        return n;
    }
}
