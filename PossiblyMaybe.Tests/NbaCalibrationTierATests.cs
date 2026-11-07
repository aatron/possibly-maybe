using PossiblyMaybe.Cli.Backtest;
using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Tests;

public class NbaCalibrationTierATests
{
    [Test]
    public async Task Registry_T4b_Variants_Present()
    {
        foreach (var id in NbaCalibrationTierA.VariantIds)
            _ = VariantRegistry.Require(id);

        await Assert.That(VariantRegistry.Require("refs-nba").UseRefTotals).IsTrue();
        await Assert.That(VariantRegistry.Require("V-FC-NBA").FittedCurve).IsTrue();
        await Assert.That(VariantRegistry.Require("V-NBA-STRICT").MinAbsEdge)
            .IsEqualTo(CalibrationFamily.NbaStrictEdge);
        var n = NbaCalibrationTierA.VariantIds.Length;
        await Assert.That(NbaCalibrationTierA.NTrials).IsEqualTo(n);
        await Assert.That(NbaCalibrationTierA.FoldSeasons).IsEquivalentTo(NbaNormalSigmaTierA.FoldSeasons);
    }

    [Test]
    public async Task KeepRule_Pass_EntersShadow_NeverPaper()
    {
        await Assert.That(NbaCalibrationTierA.Keep(meanClvVariant: 0.5, meanClvV0: 0.4, totalBets: 40))
            .IsTrue();
        await Assert.That(NbaCalibrationTierA.Keep(meanClvVariant: 0.3, meanClvV0: 0.4, totalBets: 100))
            .IsFalse();
        await Assert.That(NbaCalibrationTierA.Keep(meanClvVariant: 1.0, meanClvV0: 0.0, totalBets: 39))
            .IsFalse();

        var stage = PromotionPolicy.EnterLiveShadow(tierAPass: true, vaultFinalPass: true);
        await Assert.That(stage).IsEqualTo(LadderStage.Shadow);
        await Assert.That(() => PromotionPolicy.RefuseSimOnlyPromotion(LadderStage.Paper))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task TrialLedger_Records_Each_Variant_Once()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pm-t4b-trials-{Guid.NewGuid():N}.db");
        try
        {
            var ledger = TrialLedger.Open(path);
            foreach (var id in NbaCalibrationTierA.VariantIds)
            {
                ledger.Record(
                    Vault.StrategyHash(id, "t4b-tier-a-once"),
                    $"{{\"id\":\"{id}\",\"tier\":\"A\",\"round\":\"5\",\"item\":\"T4b\"}}",
                    NbaCalibrationTierA.FoldSeasons[0],
                    NbaCalibrationTierA.FoldSeasons[^1],
                    gridSize: 1);
            }

            await Assert.That(ledger.NTrials).IsEqualTo(NbaCalibrationTierA.NTrials);
            var reopened = TrialLedger.Open(path);
            await Assert.That(reopened.NTrials).IsEqualTo(NbaCalibrationTierA.NTrials);
            await Assert.That(reopened.FormatReportFooter()).Contains($"N_trials={NbaCalibrationTierA.NTrials}");
        }
        finally
        {
            try { File.Delete(path); } catch { /* best-effort */ }
        }
    }

    [Test]
    public async Task FoldSmoke_2019_HasV0Bets_WhenDbPresent()
    {
        var db = ResolveDb();
        if (db is null)
        {
            Console.WriteLine("SKIP T4b fold smoke: no possiblymaybe.db");
            return;
        }

        var (v0, refs, fitted) = await NbaCalibrationTierA.RunFoldSmokeAsync(db, "2019");
        Console.WriteLine(
            $"fold2019 V0 n={v0.Bets} clv={v0.MeanClv:F2} br={v0.Brier:F4} | " +
            $"refs n={refs.Bets} clv={refs.MeanClv:F2} | fc n={fitted.Bets} clv={fitted.MeanClv:F2} br={fitted.Brier:F4}");
        await Assert.That(v0.Bets).IsGreaterThan(0);
        // Fitted curve uses locked σ — Brier may differ from expanding-σ V0.
        await Assert.That(fitted.GamesScored).IsEqualTo(v0.GamesScored);
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
}
