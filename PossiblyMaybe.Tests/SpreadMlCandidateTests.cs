using PossiblyMaybe.Cli.Variants;
using PossiblyMaybe.Data;
using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Tests;

public class SpreadMlCandidateTests
{
    [Test]
    public async Task BuildForGame_EmitsMlAndSpread_SkipsOregon()
    {
        var snaps = new List<OddsSnapshot>
        {
            new() { Market = "ML", Side = "home", PriceKind = PriceKind.Open, AmericanOdds = -150 },
            new() { Market = "ML", Side = "away", PriceKind = PriceKind.Open, AmericanOdds = +130 },
            new() { Market = "SPREAD", Side = "home", PriceKind = PriceKind.Open, AmericanOdds = -110, Line = -4.5 },
            new() { Market = "SPREAD", Side = "away", PriceKind = PriceKind.Open, AmericanOdds = -110, Line = 4.5 },
        };

        var ok = SpreadMlCandidateBackfill.BuildForGame("NBA", "20261107", "BOS", "MIA", 0.62, snaps).ToList();
        await Assert.That(ok.Count).IsEqualTo(2);
        await Assert.That(ok.Select(c => c.Market).Order().ToArray()).IsEquivalentTo(["ML", "SPREAD"]);
        await Assert.That(ok.Single(c => c.Market == "ML").ModelExp).IsEqualTo(0.62);
        await Assert.That(ok.Single(c => c.Market == "SPREAD").Line).IsEqualTo(-4.5);

        var oregon = SpreadMlCandidateBackfill.BuildForGame("NBA", "20261107", "POR", "LAL", 0.55, snaps).ToList();
        await Assert.That(oregon).IsEmpty();

        var ncaa = SpreadMlCandidateBackfill.BuildForGame("NCAAF", "20261107", "ORE", "UCLA", 0.55, snaps).ToList();
        await Assert.That(ncaa).IsEmpty();
    }

    [Test]
    public async Task Evaluate_WrongMarket_OnSpreadCandidate()
    {
        var c = SpreadCand("BOS", "MIA", -4.5, expMargin: 6.0);
        var d = VariantRegistry.Evaluate(VariantRegistry.V0, c);
        await Assert.That(d.Pick).IsFalse();
        await Assert.That(d.SkipReason).IsEqualTo(VariantSkip.WrongMarket);
    }

    [Test]
    public async Task CandidateStore_RoundTrip_PerMarket()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pm-c7-{Guid.NewGuid():N}.db");
        try
        {
            var total = new SlateCandidate(
                "NBA", "20261107", "BOS", "MIA", 232, null, null,
                220, 220, -110, -110, 15,
                ChampConfig.MarketWeight, ChampConfig.MinAbsTotalEdge, ChampConfig.MinEv, false,
                Market: "TOTAL");
            var spread = new SlateCandidate(
                "NBA", "20261107", "BOS", "MIA", 5.0, null, null,
                -4.5, -4.5, -110, -110, 0,
                SpreadMlCandidateBackfill.MarketWeight,
                SpreadMlCandidateBackfill.MinAbsSpreadEdge,
                SpreadMlCandidateBackfill.MinSpreadEv, false,
                Market: "SPREAD");
            var ml = MlCand("BOS", "MIA", 0.61, -150, 130);

            var n = await VariantEvaluate.RecordAsync(path, [total, spread, ml]);
            // Only TOTAL gets shadow variant writes.
            await Assert.That(n).IsEqualTo(VariantRegistry.All.Count);

            var (db, _) = await VariantLedger.OpenAsync(path);
            await using (db)
            {
                var rows = db.CandidateEntries.OrderBy(r => r.Market).ToList();
                await Assert.That(rows.Count).IsEqualTo(3);
                await Assert.That(rows.Select(r => r.Market).ToArray()).IsEquivalentTo(["ML", "SPREAD", "TOTAL"]);
                await Assert.That(rows.All(r => r.GameKey == "20261107 MIA@BOS")).IsTrue();

                var slateSp = VariantLedger.ToSlate(rows.Single(r => r.Market == "SPREAD"));
                await Assert.That(slateSp.Market).IsEqualTo("SPREAD");
                await Assert.That(slateSp.Line).IsEqualTo(-4.5);
            }
        }
        finally
        {
            try { File.Delete(path); } catch { /* */ }
        }
    }

    [Test]
    public async Task TierA_NbaMlAndSpread_HoldoutHasBets()
    {
        var db = ResolveDb();
        if (db is null)
        {
            Console.WriteLine("SKIP C7 Tier A: no possiblymaybe.db");
            return;
        }

        var (mlTrain, mlHold) = await PossiblyMaybe.Cli.Backtest.Gate1ComboBacktest.RunAsync(
            db, PossiblyMaybe.Cli.Backtest.BetMarketMode.Moneyline, sport: "NBA");
        var (spTrain, spHold) = await PossiblyMaybe.Cli.Backtest.Gate1ComboBacktest.RunAsync(
            db, PossiblyMaybe.Cli.Backtest.BetMarketMode.Spread, sport: "NBA");

        Console.WriteLine(
            $"C7 ML train bets={mlTrain.Bets} hold bets={mlHold.Bets} meanClv={mlHold.MeanMlClv:F4} pnl={mlHold.PaperPnlUnits:F1}");
        Console.WriteLine(
            $"C7 SP train bets={spTrain.Bets} hold bets={spHold.Bets} meanClvPts={spHold.MeanSpreadClvPts:F3} pnl={spHold.PaperPnlUnits:F1}");

        await Assert.That(mlTrain.Bets).IsGreaterThan(0);
        await Assert.That(mlHold.Bets).IsGreaterThan(0);
        await Assert.That(spTrain.Bets).IsGreaterThan(0);
        await Assert.That(spHold.Bets).IsGreaterThan(0);
        await Assert.That(mlTrain.GamesScored).IsGreaterThan(1000);
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

    static SlateCandidate SpreadCand(string home, string away, double line, double expMargin)
        => new("NBA", "20261107", home, away, expMargin, null, null,
            line, line, -110, -110, 0,
            SpreadMlCandidateBackfill.MarketWeight,
            SpreadMlCandidateBackfill.MinAbsSpreadEdge,
            SpreadMlCandidateBackfill.MinSpreadEv, false,
            Market: "SPREAD");

    static SlateCandidate MlCand(string home, string away, double p, int homeJuice, int awayJuice)
        => new("NBA", "20261107", home, away, p, null, null,
            null, null, homeJuice, awayJuice, 0,
            SpreadMlCandidateBackfill.MarketWeight, 0, SpreadMlCandidateBackfill.MinMlEv, false,
            Market: "ML");
}
