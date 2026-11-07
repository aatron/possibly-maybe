using PossiblyMaybe.Cli.Replay;
using PossiblyMaybe.Domain;
using PossiblyMaybe.Domain.SportPacks;

namespace PossiblyMaybe.Tests;

public class VaultTests
{
    static readonly DateTimeOffset T = DateTimeOffset.Parse("2025-07-01T00:00:00Z");
    const string VaultDay = "20250701"; // MLS TierC holdout season 2025

    [Test]
    public async Task Vault_Double_Use_Refused_For_Same_Hash_And_League()
    {
        var store = new VaultStore();
        var hash = Vault.StrategyHash("V0", "{}");
        Vault.AuthorizeSimulationDate(store, "MLS", VaultDay, hash, final: true);
        await Assert.That(store.HasUsed("MLS", hash)).IsTrue();

        await Assert.That(() => Vault.AuthorizeSimulationDate(store, "MLS", VaultDay, hash, final: true))
            .Throws<VaultAlreadyUsedException>();
    }

    [Test]
    public async Task Engine_Sim_Refuses_Vault_Date_Without_Final()
    {
        var slate = new List<SlateCandidate> { MlsCand(VaultDay) };
        var view = new AsOfView(T);
        var vault = new VaultStore();
        var trials = new TrialLedger();
        var hash = Vault.StrategyHash("V0");
        var opts = EngineRunOptions.Sim(hash, vault, trials, final: false);

        await Assert.That(() => DayEngine.Run(VaultDay, StrategyRegistry.V0, view, slate, opts))
            .Throws<VaultSealedException>();
        await Assert.That(trials.NTrials).IsEqualTo(0);
    }

    [Test]
    public async Task Engine_Sim_Final_Then_Second_Final_Refused_And_Report_Shows_NTrials()
    {
        var slate = new List<SlateCandidate> { MlsCand(VaultDay) };
        var view = new AsOfView(T);
        var vault = new VaultStore();
        var trials = new TrialLedger();
        var hash = Vault.StrategyHash("V0", "p=1");
        var opts = EngineRunOptions.Sim(hash, vault, trials, final: true, paramsJson: "p=1",
            rangeFrom: VaultDay, rangeTo: VaultDay);

        var result = DayEngine.Run(VaultDay, StrategyRegistry.V0, view, slate, opts);
        await Assert.That(result.TrialFooter).IsNotNull();
        await Assert.That(result.TrialFooter!).Contains("N_trials=1");
        await Assert.That(result.TrialFooter!).Contains("DSR=");
        await Assert.That(trials.NTrials).IsEqualTo(1);

        await Assert.That(() => DayEngine.Run(VaultDay, StrategyRegistry.V0, view, slate, opts))
            .Throws<VaultAlreadyUsedException>();
    }

    [Test]
    public async Task Nested_WalkForward_Counts_Grid_As_Trials_And_Skips_Vault_Fold()
    {
        var pack = SportPackRegistry.Mls;
        var folds = NestedWalkForward.TrainFolds(pack);
        await Assert.That(folds).DoesNotContain(pack.TierCHoldoutSeason);
        await Assert.That(folds).DoesNotContain(pack.SkipSeason);

        var ledger = new TrialLedger();
        var hash = Vault.StrategyHash("toy");
        var grid = new[] { 0.1, 0.2, 0.3 };
        var best = NestedWalkForward.SelectOnTrain(
            folds.Take(2).ToList(),
            grid,
            (p, fold) => p == 0.2 ? 10 : 1, // 0.2 wins on every fold
            ledger,
            hash,
            "edge-grid",
            "2019",
            "2024");

        await Assert.That(best).IsEqualTo(0.2);
        await Assert.That(ledger.NTrials).IsEqualTo(3);
        await Assert.That(ledger.FormatReportFooter(observedSharpe: 1.0, nReturns: 50))
            .Contains("N_trials=3");
    }

    [Test]
    public async Task Promotion_From_Sim_Enters_Shadow_Only_Never_Paper()
    {
        var stage = PromotionPolicy.EnterLiveShadow(tierAPass: true, vaultFinalPass: true);
        await Assert.That(stage).IsEqualTo(LadderStage.Shadow);

        await Assert.That(() => PromotionPolicy.RefuseSimOnlyPromotion(LadderStage.Paper))
            .Throws<InvalidOperationException>();
        await Assert.That(() => PromotionPolicy.RefuseSimOnlyPromotion(LadderStage.RealCandidate))
            .Throws<InvalidOperationException>();
        await Assert.That(() => PromotionPolicy.EnterLiveShadow(false, true))
            .Throws<InvalidOperationException>();
    }

    static SlateCandidate MlsCand(string day)
        => new("MLS", day, "LAFC", "SEA", 3.3, 1.8, 1.5,
            2.5, 2.5, -110, -110, 0,
            MlsChampConfig.MarketWeight, MlsChampConfig.MinAbsTotalEdge, MlsChampConfig.MinEv, false);
}
