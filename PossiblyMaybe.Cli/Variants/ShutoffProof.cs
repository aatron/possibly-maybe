using Microsoft.EntityFrameworkCore;
using PossiblyMaybe.Data;
using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Cli.Variants;

/// <summary>
/// Chronological V3 (signal) bet stream → compare flat PnL / max DD with vs without shutoff.
/// </summary>
public static class ShutoffProof
{
    public sealed record LeagueRow(string Sport, int N, double PnlV3, double DdV3, double PnlV4, double DdV4, int TakenV4);

    public static async Task<IReadOnlyList<LeagueRow>> RunAsync(string warehouseDbPath, CancellationToken ct = default)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={warehouseDbPath}").Options;
        await using var wdb = new AppDbContext(options);
        var rows = new List<LeagueRow>
        {
            await NbaAsync(wdb, ct),
        };
        foreach (var sport in new[] { "MLS", "LIGUE1", "LIGAMX", "SERIEA", "LALIGA", "EFL" })
        foreach (var sport in new[] { "MLS", "LIGUE1", "LIGAMX", "SERIEA", "EPL" })
            rows.Add(await SoccerAsync(wdb, sport, ct));
        return rows;
    }

    public static void Print(IReadOnlyList<LeagueRow> rows)
    {
        Console.WriteLine("sport\tn\tpnlV3\tddV3\tpnlV4\tddV4\ttakenV4\tddBetter\tpnlOk");
        foreach (var r in rows)
        {
            var ddBetter = r.DdV4 <= r.DdV3 + 1e-9;
            var pnlOk = r.PnlV4 + 1e-9 >= r.PnlV3;
            Console.WriteLine(
                $"{r.Sport}\t{r.N}\t{r.PnlV3:F1}\t{r.DdV3:F1}\t{r.PnlV4:F1}\t{r.DdV4:F1}\t{r.TakenV4}\t{ddBetter}\t{pnlOk}");
        }
    }

    private static async Task<LeagueRow> NbaAsync(AppDbContext wdb, CancellationToken ct)
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
        var signal = new List<(double Clv, double Pnl)>();
        var v3 = VariantRegistry.Require(Guards.ShutoffSignalVariantId);

        foreach (var g in games)
        {
            byGame.TryGetValue(g.Id, out var snaps);
            snaps ??= [];
            var open = snaps.FirstOrDefault(s => s.PriceKind == PriceKind.Open);
            var close = snaps.FirstOrDefault(s => s.PriceKind == PriceKind.Close);
            var modelExp = pace.ExpectedTotal(g.HomeTeam, g.AwayTeam);
            var day = g.TipoffUtc.UtcDateTime.ToString("yyyyMMdd");

            if (open?.Line is { } line && g.HomeScore is { } hs && g.AwayScore is { } as_)
            {
                var c = new SlateCandidate(
                    "NBA", day, g.HomeTeam, g.AwayTeam,
                    modelExp, null, null,
                    line, line,
                    open.AmericanOdds == 0 ? -110 : open.AmericanOdds,
                    open.AmericanOdds == 0 ? -110 : open.AmericanOdds,
                    resid.Sigma,
                    ChampConfig.MarketWeight, ChampConfig.MinAbsTotalEdge, ChampConfig.MinEv,
                    Stale: false);
                var d = VariantRegistry.Evaluate(v3, c);
                if (d is { Pick: true, Side: not null, Line: not null })
                {
                    var closeLine = close?.Line ?? line;
                    var overClv = TotalsMath.OverClvPoints(d.Line.Value, closeLine);
                    var clv = d.Side.Equals("OVER", StringComparison.OrdinalIgnoreCase) ? overClv : -overClv;
                    var ou = TotalsMath.OuResult(hs, as_, d.Line.Value);
                    double pnl;
                    if (ou == 0) pnl = 0;
                    else if ((d.Side.Equals("OVER", StringComparison.OrdinalIgnoreCase) && ou > 0)
                             || (d.Side.Equals("UNDER", StringComparison.OrdinalIgnoreCase) && ou < 0))
                        pnl = TotalsMath.OuPnL(d.AmericanOdds, 1);
                    else
                        pnl = -1;
                    signal.Add((clv, pnl));
                }

                var exp = ChampConfig.MarketWeight * line + (1 - ChampConfig.MarketWeight) * modelExp;
                resid.Observe(hs + as_ - exp);
            }

            if (boxes.TryGetValue(g.Id, out var b))
            {
                pace.Update(g.HomeTeam, g.AwayTeam,
                    b.FgaHome, b.OrebHome, b.TovHome, b.FtaHome, b.PtsHome, b.Fg3aHome,
                    b.FgaAway, b.OrebAway, b.TovAway, b.FtaAway, b.PtsAway, b.Fg3aAway);
            }
        }

        return Row("NBA", signal);
    }

    private static async Task<LeagueRow> SoccerAsync(AppDbContext wdb, string sport, CancellationToken ct)
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
        var signal = new List<(double Clv, double Pnl)>();
        var v3 = VariantRegistry.Require(Guards.ShutoffSignalVariantId);

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
            if (open?.Line is { } line && g.HomeScore is { } hs && g.AwayScore is { } as_)
            {
                var c = new SlateCandidate(
                    sport, day, g.HomeTeam, g.AwayTeam,
                    modelExp, lh, la,
                    line, line,
                    -110, -110,
                    0, w, minEdge, minEv, Stale: false);
                var d = VariantRegistry.Evaluate(v3, c);
                if (d is { Pick: true, Side: not null, Line: not null })
                {
                    var closeLine = close?.Line ?? line;
                    var overClv = TotalsMath.OverClvPoints(d.Line.Value, closeLine);
                    var clv = d.Side.Equals("OVER", StringComparison.OrdinalIgnoreCase) ? overClv : -overClv;
                    var ou = TotalsMath.OuResult(hs, as_, d.Line.Value);
                    double pnl;
                    if (ou == 0) pnl = 0;
                    else if ((d.Side.Equals("OVER", StringComparison.OrdinalIgnoreCase) && ou > 0)
                             || (d.Side.Equals("UNDER", StringComparison.OrdinalIgnoreCase) && ou < 0))
                        pnl = TotalsMath.OuPnL(d.AmericanOdds, 1);
                    else
                        pnl = -1;
                    signal.Add((clv, pnl));
                }
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

        return Row(sport, signal);
    }

    private static LeagueRow Row(string sport, List<(double Clv, double Pnl)> signal)
    {
        var v3 = ShutoffSim.Run(signal, shutoff: false);
        var v4 = ShutoffSim.Run(signal, shutoff: true);
        return new LeagueRow(sport, signal.Count, v3.Pnl, v3.MaxDrawdown, v4.Pnl, v4.MaxDrawdown, v4.Taken);
    }
}
