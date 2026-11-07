using PossiblyMaybe.Cli.Backtest;
using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Tests;

public class SoccerR5TierATests
{
    [Test]
    public async Task Pooling_Phases_Include_A5_Openers_And_LigaMx()
    {
        await Assert.That(SoccerR5TierA.IsPoolingPhase("opening-weeks")).IsTrue();
        await Assert.That(SoccerR5TierA.IsPoolingPhase("promoted")).IsTrue();
        await Assert.That(SoccerR5TierA.IsPoolingPhase("apertura-opening")).IsTrue();
        await Assert.That(SoccerR5TierA.IsPoolingPhase("clausura-opening")).IsTrue();
        await Assert.That(SoccerR5TierA.IsPoolingPhase("playoffs")).IsFalse();
        await Assert.That(SoccerR5TierA.IsPoolingPhase(null)).IsFalse();
    }

    [Test]
    public async Task Variant_Ids_Match_B4_And_B2_Spec()
    {
        await Assert.That(SoccerR5TierA.VariantIds).IsEquivalentTo(
            new[] { "mls-dc", "+poisson-all", "+negbin", "+dc-rho", "+pooling" });
        await Assert.That(SoccerR5TierA.Sports).IsEquivalentTo(
            new[] { "MLS", "LIGUE1", "LIGAMX", "SERIEA" });
    }

    [Test]
    public async Task TrialLedger_Records_Each_Variant_Run_Durably()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pm-t4a-vault-{Guid.NewGuid():N}.db");
        try
        {
            var (_, trials) = VaultDb.Open(path);
            foreach (var id in SoccerR5TierA.VariantIds)
            {
                var hash = Vault.StrategyHash($"t4a:MLS:{id}", $"variant={id}");
                trials.Record(hash, $"variant={id}", "2019", "2024", gridSize: 1);
            }

            var (_, reload) = VaultDb.Open(path);
            await Assert.That(reload.NTrials).IsEqualTo(SoccerR5TierA.VariantIds.Length);
            await Assert.That(reload.FormatReportFooter()).Contains("N_trials=5");
            await Assert.That(reload.FormatReportFooter()).Contains("DSR=");
            await Assert.That(reload.FormatReportFooter()).Contains("BH_q=");
        }
        finally
        {
            try { File.Delete(path); } catch { /* temp cleanup */ }
        }
    }

    [Test]
    public async Task Promotion_From_TierA_Pass_Is_Shadow_Only()
    {
        var stage = PromotionPolicy.EnterLiveShadow(tierAPass: true, vaultFinalPass: true);
        await Assert.That(stage).IsEqualTo(LadderStage.Shadow);
        await Assert.That(() => PromotionPolicy.RefuseSimOnlyPromotion(LadderStage.Paper))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task OregonExclusion_Still_Blocks_Portland_On_Mls()
    {
        await Assert.That(OregonExclusion.IsExcluded("MLS", "Portland Timbers", "LAFC")).IsTrue();
        await Assert.That(OregonExclusion.IsExcluded("MLS", "Seattle Sounders", "LAFC")).IsFalse();
    }
}
