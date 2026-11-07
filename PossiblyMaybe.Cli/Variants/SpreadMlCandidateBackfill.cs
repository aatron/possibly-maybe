using Microsoft.EntityFrameworkCore;
using PossiblyMaybe.Cli.Backtest;
using PossiblyMaybe.Data;
using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Cli.Variants;

/// <summary>
/// C7: emit CandidateEntries for SPREAD + ML where the warehouse has those markets.
/// Sports scanned: NBA, NFL, NCAAF, NCAAB (today only NBA has ML/SPREAD rows).
/// OregonExclusion skips both sides before upsert. No new tables — Market column only.
/// </summary>
public static class SpreadMlCandidateBackfill
{
    public static readonly string[] Sports = ["NBA", "NFL", "NCAAF", "NCAAB"];

    public const double MarketWeight = 0.7;
    public const double MinAbsSpreadEdge = 1.0;
    public const double MinSpreadEv = 0.05;
    public const double MinMlEv = 0.03;

    public static async Task<IReadOnlyDictionary<string, int>> RunAsync(
        string warehouseDbPath,
        string variantsDbPath,
        CancellationToken ct = default)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var (vdb, _) = await VariantLedger.OpenAsync(variantsDbPath, ct);
        await using (vdb)
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={warehouseDbPath}").Options;
            await using var wdb = new AppDbContext(options);

            var present = (await wdb.Games.AsNoTracking()
                    .Select(g => g.Sport).Distinct().ToListAsync(ct))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var sport in Sports)
            {
                if (!present.Contains(sport))
                {
                    Console.WriteLine($"spreadml-backfill: {sport} — no games, skip");
                    counts[sport] = 0;
                    continue;
                }

                var n = await BackfillSportAsync(wdb, vdb, sport, ct);
                counts[sport] = n;
                Console.WriteLine($"spreadml-backfill: {sport} candidate rows≈{n}");
            }
        }

        return counts;
    }

    /// <summary>Build in-memory ML/SPREAD candidates for one scored game (tests + backfill).</summary>
    public static IEnumerable<SlateCandidate> BuildForGame(
        string sport,
        string yyyymmdd,
        string home,
        string away,
        double homeWinProb,
        IReadOnlyList<OddsSnapshot> snaps)
    {
        if (OregonExclusion.IsExcluded(sport, home, away))
            yield break;

        var ml = snaps.Where(s => s.Market == "ML").ToList();
        var openHomeMl = Pick(ml, "home", PriceKind.Open);
        var openAwayMl = Pick(ml, "away", PriceKind.Open);
        if (openHomeMl is not null && openAwayMl is not null)
        {
            yield return new SlateCandidate(
                sport, yyyymmdd, home, away,
                homeWinProb, null, null,
                null, null,
                openHomeMl.AmericanOdds == 0 ? -110 : openHomeMl.AmericanOdds,
                openAwayMl.AmericanOdds == 0 ? -110 : openAwayMl.AmericanOdds,
                0, MarketWeight, 0, MinMlEv, false,
                Market: "ML");
        }

        var sp = snaps.Where(s => s.Market == "SPREAD").ToList();
        var openHomeSp = Pick(sp, "home", PriceKind.Open);
        var openAwaySp = Pick(sp, "away", PriceKind.Open);
        if (openHomeSp?.Line is { } homeLine)
        {
            var expMargin = SpreadMath.ExpectedHomeMargin(homeWinProb);
            var juiceH = openHomeSp.AmericanOdds == 0 ? -110 : openHomeSp.AmericanOdds;
            var juiceA = openAwaySp is null || openAwaySp.AmericanOdds == 0
                ? -110
                : openAwaySp.AmericanOdds;
            yield return new SlateCandidate(
                sport, yyyymmdd, home, away,
                expMargin, null, null,
                homeLine, homeLine,
                juiceH, juiceA,
                0, MarketWeight, MinAbsSpreadEdge, MinSpreadEv, false,
                Market: "SPREAD");
        }
    }

    static async Task<int> BackfillSportAsync(
        AppDbContext wdb, VariantDbContext vdb, string sport, CancellationToken ct)
    {
        var games = (await wdb.Games.AsNoTracking()
                .Where(g => g.Sport == sport && g.HomeScore != null && g.AwayScore != null)
                .ToListAsync(ct))
            .OrderBy(g => g.TipoffUtc).ThenBy(g => g.Id)
            .ToList();
        if (games.Count == 0) return 0;

        // Join by sport — avoid Contains(huge id set) (SQLite variable limit on NCAA).
        var odds = await (
            from o in wdb.OddsSnapshots.AsNoTracking()
            join g in wdb.Games.AsNoTracking() on o.GameId equals g.Id
            where g.Sport == sport && (o.Market == "ML" || o.Market == "SPREAD")
            select o).ToListAsync(ct);
        if (odds.Count == 0)
        {
            Console.WriteLine($"  {sport}: no ML/SPREAD odds in warehouse");
            return 0;
        }

        var byGame = odds.GroupBy(o => o.GameId).ToDictionary(g => g.Key, g => g.ToList());
        var refs = await (
            from r in wdb.RefereeAssignments.AsNoTracking()
            join g in wdb.Games.AsNoTracking() on r.GameId equals g.Id
            where g.Sport == sport
            select r).ToListAsync(ct);
        var byRefs = refs.GroupBy(r => r.GameId).ToDictionary(g => g.Key, g => g.ToList());

        var elo = new EloBook();
        var refFx = new RefEffects();
        var rest = new RestTracker();
        var n = 0;

        foreach (var g in games)
        {
            byGame.TryGetValue(g.Id, out var snaps);
            snaps ??= [];
            byRefs.TryGetValue(g.Id, out var crew);
            crew ??= [];
            var homeWon = g.HomeScore > g.AwayScore;
            var p = HomeWinProb(g, snaps, crew, elo, refFx, rest);
            var day = g.TipoffUtc.UtcDateTime.ToString("yyyyMMdd");

            foreach (var c in BuildForGame(sport, day, g.HomeTeam, g.AwayTeam, p, snaps))
            {
                await VariantLedger.UpsertCandidateAsync(vdb, c, ct);
                n++;
            }

            refFx.Observe(crew.Select(c => c.OfficialName), homeWon, crew.Any(x => x.DonaghyExcludeFromFit));
            elo.Update(g.HomeTeam, g.AwayTeam, homeWon);
            var d = DateOnly.FromDateTime(g.TipoffUtc.UtcDateTime);
            rest.Played(g.HomeTeam, d);
            rest.Played(g.AwayTeam, d);
        }

        return n;
    }

    static double HomeWinProb(
        Game g, List<OddsSnapshot> snaps, List<RefereeAssignment> crew,
        EloBook elo, RefEffects refFx, RestTracker rest)
    {
        const double refWeight = 0.5, loadWeight = 0.3;
        var ml = snaps.Where(s => s.Market == "ML").ToList();
        var closeHome = Pick(ml, "home", PriceKind.Close);
        var closeAway = Pick(ml, "away", PriceKind.Close);
        var openHome = Pick(ml, "home", PriceKind.Open);
        var openAway = Pick(ml, "away", PriceKind.Open);
        double mHome = 0.5;
        if (closeHome is not null && closeAway is not null)
        {
            var ha = openHome?.AmericanOdds ?? closeHome.AmericanOdds;
            var aa = openAway?.AmericanOdds ?? closeAway.AmericanOdds;
            (mHome, _) = OddsMath.NoVigTwoWay(ha, aa);
        }

        var p = MarketWeight * mHome + (1 - MarketWeight) * elo.HomeWinProbability(g.HomeTeam, g.AwayTeam);
        p = Math.Clamp(p + refWeight * refFx.CrewHomeBias(crew.Select(c => c.OfficialName)), 0.01, 0.99);
        var load = rest.HomeRestEdge(g.HomeTeam, g.AwayTeam, DateOnly.FromDateTime(g.TipoffUtc.UtcDateTime));
        return Math.Clamp(p + loadWeight * load, 0.01, 0.99);
    }

    static OddsSnapshot? Pick(List<OddsSnapshot> snaps, string side, PriceKind kind)
        => snaps.Where(s => s.Side == side && s.PriceKind == kind)
            .OrderBy(s => s.ClvQuality)
            .ThenBy(s => s.Book is Book.Consensus or Book.Mgm ? 0 : 1)
            .FirstOrDefault();
}
