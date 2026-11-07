using PossiblyMaybe.Cli.Backtest;
using PossiblyMaybe.Cli.Replay;
using PossiblyMaybe.Domain;
using PossiblyMaybe.Domain.SportPacks;

namespace PossiblyMaybe.Tests;

public class T6TierATests
{
    [Test]
    public async Task Nhl_TierC_Vault_Burn_Is_Once_Only_On_Durable_Store()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pm-t6-vault-{Guid.NewGuid():N}.db");
        try
        {
            var pack = NhlSportPack.Instance;
            var hash = Vault.StrategyHash(NhlTotalsTierA.StrategyId, "t6-test");
            var (vault1, trials1) = VaultDb.Open(path);
            var opts = EngineRunOptions.Sim(hash, vault1, trials1, final: true,
                paramsJson: "t6-test",
                rangeFrom: NhlTotalsTierA.TierCVaultDay,
                rangeTo: NhlTotalsTierA.TierCVaultDay);
            var slate = new List<SlateCandidate>
            {
                new("NHL", NhlTotalsTierA.TierCVaultDay, "BOS", "NYR", 6.0, 3.0, 3.0,
                    6.0, 6.0, -110, -110, 0,
                    pack.MarketWeight, pack.MinAbsTotalEdge, pack.MinEv, false),
            };
            var view = new AsOfView(DateTimeOffset.Parse("2022-01-15T00:00:00Z"));
            _ = DayEngine.Run(NhlTotalsTierA.TierCVaultDay, StrategyRegistry.V0, view, slate, opts);
            await Assert.That(vault1.HasUsed("NHL", hash)).IsTrue();

            var (vault2, _) = VaultDb.Open(path);
            await Assert.That(vault2.HasUsed("NHL", hash)).IsTrue();
            await Assert.That(() =>
                    Vault.AuthorizeSimulationDate(vault2, "NHL", NhlTotalsTierA.TierCVaultDay, hash, final: true))
                .Throws<VaultAlreadyUsedException>();
        }
        finally
        {
            try { File.Delete(path); } catch { /* temp cleanup */ }
        }
    }

    [Test]
    public async Task Nhl_InferSeason_Maps_VaultDay_To_TierC()
    {
        var pack = NhlSportPack.Instance;
        await Assert.That(Vault.InferSeason(pack, NhlTotalsTierA.TierCVaultDay))
            .IsEqualTo(pack.TierCHoldoutSeason);
        await Assert.That(Vault.IsSealed(pack, pack.TierCHoldoutSeason)).IsTrue();
    }

    [Test]
    public async Task Nfl_Folds_Include_2022_And_Exclude_Holdout()
    {
        var pack = NflSportPack.Instance;
        await Assert.That(pack.TierAFolds.Contains("2022")).IsTrue();
        await Assert.That(NestedWalkForward.TrainFolds(pack).Contains(pack.TierCHoldoutSeason)).IsFalse();
        await Assert.That(NestedWalkForward.TrainFolds(pack).Contains("2022")).IsTrue();
    }
}
