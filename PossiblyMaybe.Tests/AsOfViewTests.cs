using System.Reflection;
using Microsoft.EntityFrameworkCore;
using PossiblyMaybe.Cli.Replay;
using PossiblyMaybe.Data;
using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Tests;

public class AsOfViewTests
{
    static readonly DateTimeOffset T = DateTimeOffset.Parse("2026-11-07T12:00:00Z");

    /// <summary>Stand-in for R8 IStrategy.Decide — fingerprint of what the view exposes.</summary>
    static string Decide(AsOfView view)
        => $"{view.FinalGames().Count}|{view.OddsSnapshots().Count}|{view.InjuryRoster().Count}|{view.Watermark()}";

    [Test]
    public async Task Leak_a_Same_Decisions_With_Or_Without_Later_Rows()
    {
        var known = new FinalGameRow(1, "NBA", "2025", DateTimeOffset.Parse("2026-11-01T00:00:00Z"),
            "BOS", "MIA", 110, 100);
        var futureFinal = new FinalGameRow(2, "NBA", "2025", DateTimeOffset.Parse("2026-11-08T00:00:00Z"),
            "BOS", "NYK", 150, 80);
        var knownOdds = new OddsAsOfRow(10, 1, "DraftKings", "TOTAL", "OVER", -110, 220,
            DateTimeOffset.Parse("2026-11-06T18:00:00Z"));
        var futureOdds = new OddsAsOfRow(11, 2, "DraftKings", "TOTAL", "OVER", -105, 225,
            DateTimeOffset.Parse("2026-11-08T10:00:00Z"));
        var knownInj = new InjuryRosterRow("NBA", "BOS", DateTimeOffset.Parse("2026-11-06T00:00:00Z"), "{}");
        var futureInj = new InjuryRosterRow("NBA", "NYK", DateTimeOffset.Parse("2026-11-09T00:00:00Z"), "{\"out\":1}");

        var without = AsOfViewFactory.FromSnapshots(T, [known], [knownOdds], [knownInj], "opening-weeks");
        var withLater = AsOfViewFactory.FromSnapshots(T,
            [known, futureFinal], [knownOdds, futureOdds], [knownInj, futureInj], "opening-weeks");

        await Assert.That(Decide(without)).IsEqualTo(Decide(withLater));
        await Assert.That(without.Watermark()).IsEqualTo(withLater.Watermark());
        await Assert.That(withLater.FinalGames().Count).IsEqualTo(1);
        await Assert.That(withLater.OddsSnapshots().Count).IsEqualTo(1);
        await Assert.That(withLater.InjuryRoster().Count).IsEqualTo(1);
    }

    [Test]
    public async Task Leak_a_Db_With_Or_Without_Later_Rows()
    {
        var basePath = Path.Combine(Path.GetTempPath(), $"pm-r7-base-{Guid.NewGuid():N}.db");
        var fullPath = Path.Combine(Path.GetTempPath(), $"pm-r7-full-{Guid.NewGuid():N}.db");
        try
        {
            await SeedWarehouse(basePath, includeFuture: false);
            await SeedWarehouse(fullPath, includeFuture: true);

            await using var baseDb = Open(basePath);
            await using var fullDb = Open(fullPath);
            var a = await AsOfViewFactory.FromDbAsync(baseDb, T, sport: "NBA", seasonPhase: "opening-weeks");
            var b = await AsOfViewFactory.FromDbAsync(fullDb, T, sport: "NBA", seasonPhase: "opening-weeks");

            await Assert.That(Decide(a)).IsEqualTo(Decide(b));
            await Assert.That(a.Watermark()).IsEqualTo(b.Watermark());
        }
        finally
        {
            TryDelete(basePath);
            TryDelete(fullPath);
        }
    }

    [Test]
    public async Task Leak_b_Cheater_Cannot_Reach_Future_Via_Public_Api()
    {
        var futureFinal = new FinalGameRow(99, "NBA", "2025", DateTimeOffset.Parse("2026-11-10T00:00:00Z"),
            "AAA", "BBB", 200, 50);
        var futureOdds = new OddsAsOfRow(99, 99, "Kalshi", "TOTAL", "UNDER", -110, 210,
            DateTimeOffset.Parse("2026-11-10T01:00:00Z"));
        var view = new AsOfView(T, [futureFinal], [futureOdds],
            [new InjuryRosterRow("NBA", "AAA", DateTimeOffset.Parse("2026-11-11T00:00:00Z"), "x")],
            seasonPhase: null);

        // Cheater strategy: only allowed to take AsOfView — nothing after T is visible.
        var seen = CheaterProbe(view);
        await Assert.That(seen.FinalIds).IsEmpty();
        await Assert.That(seen.OddsIds).IsEmpty();
        await Assert.That(seen.InjuryTeams).IsEmpty();
    }

    [Test]
    public async Task Leak_b_Api_Surface_Has_No_DbContext()
    {
        var t = typeof(AsOfView);
        await Assert.That(t.IsSealed).IsTrue();

        foreach (var m in t.GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            var types = MemberTypes(m);
            foreach (var ct in types)
            {
                var name = ct.FullName ?? ct.Name;
                await Assert.That(name.Contains("DbContext", StringComparison.Ordinal)).IsFalse();
                await Assert.That(name.Contains("AppDbContext", StringComparison.Ordinal)).IsFalse();
                await Assert.That(name.Contains("EntityFramework", StringComparison.Ordinal)).IsFalse();
            }
        }

        // Domain must not reference EF — strategies live against Domain AsOfView only.
        var domainRefs = typeof(AsOfView).Assembly.GetReferencedAssemblies().Select(a => a.Name ?? "");
        await Assert.That(domainRefs.Any(n =>
            n.Contains("EntityFramework", StringComparison.OrdinalIgnoreCase))).IsFalse();
    }

    /// <summary>Compile-shaped probe: signature takes only AsOfView (no DB).</summary>
    static (int[] FinalIds, int[] OddsIds, string[] InjuryTeams) CheaterProbe(AsOfView view)
        => (
            view.FinalGames().Select(g => g.Id).ToArray(),
            view.OddsSnapshots().Select(o => o.Id).ToArray(),
            view.InjuryRoster().Select(i => i.Team).ToArray());

    static IEnumerable<Type> MemberTypes(MemberInfo m) => m switch
    {
        PropertyInfo p => [p.PropertyType],
        FieldInfo f => [f.FieldType],
        MethodInfo method => method.GetParameters().Select(p => p.ParameterType)
            .Append(method.ReturnType),
        ConstructorInfo c => c.GetParameters().Select(p => p.ParameterType),
        _ => [],
    };

    static async Task SeedWarehouse(string path, bool includeFuture)
    {
        await AppDbContext.EnsureCreatedAsync($"Data Source={path}");
        await using var db = Open(path);
        db.Games.Add(new Game
        {
            Sport = "NBA",
            Season = "2025",
            SourceKey = $"nba:20261101:BOS:MIA:{(includeFuture ? "f" : "b")}",
            TipoffUtc = DateTimeOffset.Parse("2026-11-01T00:00:00Z"),
            HomeTeam = "BOS",
            AwayTeam = "MIA",
            HomeScore = 110,
            AwayScore = 100,
        });
        if (includeFuture)
        {
            db.Games.Add(new Game
            {
                Sport = "NBA",
                Season = "2025",
                SourceKey = "nba:20261108:BOS:NYK",
                TipoffUtc = DateTimeOffset.Parse("2026-11-08T00:00:00Z"),
                HomeTeam = "BOS",
                AwayTeam = "NYK",
                HomeScore = 150,
                AwayScore = 80,
            });
        }

        await db.SaveChangesAsync();
        var pastId = await db.Games.Where(g => g.HomeTeam == "MIA" || g.AwayTeam == "MIA")
            .Select(g => g.Id).FirstAsync();
        db.OddsSnapshots.Add(new OddsSnapshot
        {
            GameId = pastId,
            Book = Book.DraftKings,
            Market = "TOTAL",
            Side = "OVER",
            AmericanOdds = -110,
            Line = 220,
            PriceKind = PriceKind.Open,
            CapturedAt = DateTimeOffset.Parse("2026-11-06T18:00:00Z"),
        });
        if (includeFuture)
        {
            var futId = await db.Games.Where(g => g.AwayTeam == "NYK").Select(g => g.Id).FirstAsync();
            db.OddsSnapshots.Add(new OddsSnapshot
            {
                GameId = futId,
                Book = Book.DraftKings,
                Market = "TOTAL",
                Side = "OVER",
                AmericanOdds = -105,
                Line = 225,
                PriceKind = PriceKind.Open,
                CapturedAt = DateTimeOffset.Parse("2026-11-08T10:00:00Z"),
            });
        }

        await db.SaveChangesAsync();
    }

    static AppDbContext Open(string path)
        => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={path}").Options);

    static void TryDelete(string path)
    {
        try { File.Delete(path); } catch { /* */ }
    }
}
