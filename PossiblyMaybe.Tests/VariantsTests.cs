using PossiblyMaybe.Cli.Variants;
using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Tests;

public class VariantsTests
{
    [Test]
    public async Task Registry_LaunchSet_ImmutableIds()
    {
        var ids = VariantRegistry.All.Select(v => v.Id).ToList();
        await Assert.That(ids).IsEquivalentTo(["V0", "V0L", "V1", "V2", "V3", "V4", "V5", "V-SA0"]);
        await Assert.That(VariantRegistry.V0.Label).IsEqualTo("champ-2026-11-07");
        await Assert.That(VariantRegistry.V0.NbaOverProb).IsEqualTo(NbaOverProbKind.NormalSigma);
        await Assert.That(VariantRegistry.Require("V0L").NbaOverProb).IsEqualTo(NbaOverProbKind.Logistic);
        await Assert.That(VariantRegistry.Require("V1").EdgeCap).IsTrue();
        await Assert.That(VariantRegistry.Require("V2").LineMove).IsTrue();
        await Assert.That(VariantRegistry.Require("V5").UseRealJuice).IsTrue();
        await Assert.That(VariantRegistry.Require("V-SA0").Leagues!.Contains("SERIEA")).IsFalse();
    }

    [Test]
    public async Task Filter_Oregon_Excluded()
    {
        var c = NbaCand(home: "POR", away: "LAL", line: 220, model: 230);
        var d = VariantRegistry.Evaluate(VariantRegistry.V0, c);
        await Assert.That(d.Pick).IsFalse();
        await Assert.That(d.SkipReason).IsEqualTo(VariantSkip.Excluded);
    }

    [Test]
    public async Task Filter_EdgeCap_WhenEvAboveThreshold()
    {
        // Huge model edge → high EV at −110 → V1 EDGE_CAP; V0 still picks.
        var c = NbaCand(home: "BOS", away: "CHA", line: 220, model: 250, sigma: 10);
        var v0 = VariantRegistry.Evaluate(VariantRegistry.V0, c);
        var v1 = VariantRegistry.Evaluate(VariantRegistry.Require("V1"), c);
        await Assert.That(v0.Pick).IsTrue();
        await Assert.That(v1.Pick).IsFalse();
        await Assert.That(v1.SkipReason).IsEqualTo(VariantSkip.EdgeCap);
    }

    [Test]
    public async Task Filter_LineMove_AgainstOver()
    {
        var c = NbaCand(home: "BOS", away: "NYK", line: 218, model: 230, open: 221.5, sigma: 12);
        var v2 = VariantRegistry.Evaluate(VariantRegistry.Require("V2"), c);
        await Assert.That(v2.Side).IsEqualTo("OVER");
        await Assert.That(v2.Pick).IsFalse();
        await Assert.That(v2.SkipReason).IsEqualTo(VariantSkip.LineMove);
    }

    [Test]
    public async Task Filter_LeaguePaused_OnV4()
    {
        var c = NbaCand(home: "BOS", away: "MIA", line: 220, model: 232, sigma: 15);
        var paused = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "NBA" };
        var v3 = VariantRegistry.Evaluate(VariantRegistry.Require("V3"), c, paused);
        var v4 = VariantRegistry.Evaluate(VariantRegistry.Require("V4"), c, paused);
        await Assert.That(v3.Pick).IsTrue();
        await Assert.That(v4.Pick).IsFalse();
        await Assert.That(v4.SkipReason).IsEqualTo(VariantSkip.LeaguePaused);
        await Assert.That(VariantRegistry.Require("V4").LeagueShutoff).IsTrue();
    }

    [Test]
    public async Task Filter_SerieA_Off_OnVSa0()
    {
        var c = new SlateCandidate(
            "SERIEA", "20261107", "Roma", "Lazio",
            2.8, 1.5, 1.3, 2.5, 2.5, -110, -110, 0,
            0.4, 0.35, 0.04, false);
        var v3 = VariantRegistry.Evaluate(VariantRegistry.Require("V3"), c);
        var vSa = VariantRegistry.Evaluate(VariantRegistry.Require("V-SA0"), c);
        await Assert.That(vSa.SkipReason).IsEqualTo(VariantSkip.LeagueOff);
        // V3 may pick or low-EV depending on poisson; just ensure not league-off
        await Assert.That(v3.SkipReason).IsNotEqualTo(VariantSkip.LeagueOff);
    }

    [Test]
    public async Task ShadowLedger_RoundTrip()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pm-variants-{Guid.NewGuid():N}.db");
        try
        {
            var c = NbaCand(home: "BOS", away: "MIA", line: 220, model: 232, sigma: 15);
            var n = await VariantEvaluate.RecordAsync(path, [c]);
            await Assert.That(n).IsEqualTo(VariantRegistry.All.Count);

            var (db, _) = await VariantLedger.OpenAsync(path);
            await using (db)
            {
                var rows = db.ShadowEntries.ToList();
                await Assert.That(rows.Count).IsEqualTo(VariantRegistry.All.Count);
                var v0 = rows.Single(r => r.VariantId == "V0");
                await Assert.That(v0.SkipReason).IsNull();
                await Assert.That(v0.Side).IsEqualTo("OVER");
                await Assert.That(v0.GameKey).IsEqualTo("20261107 MIA@BOS");
            }
        }
        finally
        {
            try { File.Delete(path); } catch { /* */ }
        }
    }

    private static SlateCandidate NbaCand(
        string home, string away, double line, double model,
        double? open = null, double sigma = 19.0)
        => new("NBA", "20261107", home, away, model, null, null,
            line, open ?? line, -110, -110, sigma,
            ChampConfig.MarketWeight, ChampConfig.MinAbsTotalEdge, ChampConfig.MinEv, false);
}
