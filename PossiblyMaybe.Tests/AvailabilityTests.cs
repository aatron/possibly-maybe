using PossiblyMaybe.Cli.Backtest;
using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Tests;

public class AvailabilityTests
{
    [Test]
    public async Task AbsenceBook_MissingPts_ScalesDnpStars()
    {
        var book = new AbsenceBook(alpha: 0.2, minMpg: 18, minPriorGames: 2);
        book.UpdateAfterGame([
            new AbsenceBook.PlayerLine("p1", "BOS", 30, 20, false),
            new AbsenceBook.PlayerLine("p1", "BOS", 32, 22, false),
        ]);
        var tip = new[]
        {
            new AbsenceBook.PlayerLine("p1", "BOS", 0, 0, true),
            new AbsenceBook.PlayerLine("p2", "BOS", 20, 8, false),
        };
        var adj = AvailabilityFamily.AbsenceAdjPts(book, tip, scale: 0.5);
        await Assert.That(adj).IsLessThan(0);
        await Assert.That(adj).IsEqualTo(book.MissingPtsAdjustment(tip, 0.5));
    }

    [Test]
    public async Task Filter_Absence_AppliesAdjPts_VsV0()
    {
        // Large negative DNP adj flips OVER edge off for +absence; V0 still picks.
        var c = Nba("BOS", "MIA", line: 220, model: 232, sigma: 19) with { AbsenceAdjPts = -20 };
        var v0 = VariantRegistry.Evaluate(VariantRegistry.V0, c);
        var abs = VariantRegistry.Evaluate(AvailabilityFamily.Absence, c);
        await Assert.That(v0.Pick).IsTrue();
        await Assert.That(v0.Side).IsEqualTo("OVER");
        await Assert.That(abs.Pick).IsFalse();
        await Assert.That(abs.SkipReason).IsEqualTo(VariantSkip.NoEdge);
    }

    [Test]
    public async Task Filter_InjuryGuard_SkipsWhenFlagged()
    {
        var c = Nba("BOS", "MIA", line: 220, model: 232, sigma: 19) with { InjuryFlag = true };
        var v0 = VariantRegistry.Evaluate(VariantRegistry.V0, c);
        var ig = VariantRegistry.Evaluate(AvailabilityFamily.InjuryGuard, c);
        await Assert.That(v0.Pick).IsTrue();
        await Assert.That(ig.Pick).IsFalse();
        await Assert.That(ig.SkipReason).IsEqualTo(VariantSkip.Injury);
    }

    [Test]
    public async Task Filter_InjuryGuard_PassesWhenUnflagged()
    {
        var c = Nba("BOS", "MIA", line: 220, model: 232, sigma: 19);
        var ig = VariantRegistry.Evaluate(AvailabilityFamily.InjuryGuard, c);
        await Assert.That(ig.Pick).IsTrue();
        await Assert.That(ig.SkipReason).IsNull();
    }

    [Test]
    public async Task ParseInjuryReport_FlagsOutWithin24h()
    {
        const string json = """
            {"injuries":[{"displayName":"Boston Celtics","injuries":[
              {"status":"Out","date":"2026-11-07T12:00:00Z"}
            ]}]}
            """;
        var report = AvailabilityFamily.ParseInjuryReport(json);
        var asOf = DateTimeOffset.Parse("2026-11-07T18:00:00Z");
        await Assert.That(AvailabilityFamily.TeamHasMaterialInjury(report, "BOS", "MIA", asOf)).IsTrue();
        await Assert.That(AvailabilityFamily.TeamHasMaterialInjury(report, "LAL", "MIA", asOf)).IsFalse();
    }

    /// <summary>Live ESPN feed — skip (no fail) when empty / unreachable.</summary>
    [Test]
    public async Task EspnInjuryFeed_Nba_SkipWhenEmpty()
    {
        var list = new List<SlateCandidate>
        {
            Nba("BOS", "MIA", line: 220, model: 232, sigma: 19),
        };
        var n = await AvailabilityFamily.ApplyEspnInjuryFlagsAsync(list);
        if (n == 0 && list[0].InjuryFlag != true)
        {
            Console.WriteLine("SKIP EspnInjuryFeed: empty or no BOS/MIA material injuries");
            return;
        }

        await Assert.That(list[0].InjuryFlag).IsTrue();
        var ig = VariantRegistry.Evaluate(AvailabilityFamily.InjuryGuard, list[0]);
        await Assert.That(ig.SkipReason).IsEqualTo(VariantSkip.Injury);
    }

    /// <summary>Tier A same-gate PaceEff vs PaceEffAbsence; skips without warehouse DB.</summary>
    [Test]
    public async Task TierA_Absence_VsPaceEff_Holdout()
    {
        var db = ResolveDb();
        if (db is null)
        {
            Console.WriteLine("SKIP TierA absence: no possiblymaybe.db");
            return;
        }

        const double w = 0.5, minEv = 0.04, minEdge = 6.0, scale = 0.5;
        var (_, paceHold) = await TotalsEdgeBacktest.RunAsync(
            db, w, minEv, minEdge, TotalsModelKind.PaceEff);
        var (_, absHold) = await TotalsEdgeBacktest.RunAsync(
            db, w, minEv, minEdge, TotalsModelKind.PaceEffAbsence, absenceScale: scale);

        Console.WriteLine(
            $"TierA absence: PaceEff n={paceHold.Bets} CLV={paceHold.MeanClv:F2} PnL={paceHold.PaperPnlUnits:F1} | " +
            $"Absence n={absHold.Bets} CLV={absHold.MeanClv:F2} PnL={absHold.PaperPnlUnits:F1}");

        // Data must move the model (player box present); do not require promote.
        await Assert.That(absHold.Bets + paceHold.Bets).IsGreaterThan(0);
    }

    static string? ResolveDb()
    {
        var here = AppContext.BaseDirectory;
        foreach (var rel in new[]
                 {
                     "possiblymaybe.db",
                     Path.Combine("..", "..", "..", "..", "possiblymaybe.db"),
                     Path.Combine("..", "..", "..", "..", "..", "possibly-maybe", "possiblymaybe.db"),
                 })
        {
            var p = Path.GetFullPath(Path.Combine(here, rel));
            if (File.Exists(p)) return p;
        }

        var env = Environment.GetEnvironmentVariable("PM_DB");
        return env is not null && File.Exists(env) ? env : null;
    }

    static SlateCandidate Nba(string home, string away, double line, double model, double sigma)
        => new("NBA", "20261107", home, away, model, null, null,
            line, line, -110, -110, sigma,
            ChampConfig.MarketWeight, ChampConfig.MinAbsTotalEdge, ChampConfig.MinEv, false);
}
