using PossiblyMaybe.Cli.Backtest;
using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Tests;

public class T4cTierATests
{
    [Test]
    public async Task KeepRule_RequiresMeanClvAndMinBets()
    {
        await Assert.That(TierAShadowVerdict.Pass(meanClvVariant: 1.0, meanClvV0: 0.5, totalBets: 40)).IsTrue();
        await Assert.That(TierAShadowVerdict.Pass(meanClvVariant: 0.4, meanClvV0: 0.5, totalBets: 100)).IsFalse();
        await Assert.That(TierAShadowVerdict.Pass(meanClvVariant: 1.0, meanClvV0: 0.5, totalBets: 39)).IsFalse();
    }

    [Test]
    public async Task Emit_RecordsDurableTrial_AndRefusesPaper()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pm-t4c-{Guid.NewGuid():N}.db");
        try
        {
            var (_, trials) = VaultDb.Open(path);
            var stage = TierAShadowVerdict.Emit(
                trials, "+conformal", pass: true,
                paramsJson: "{\"family\":\"b8\",\"tier\":\"A\"}",
                dateFrom: "2015", dateTo: "2022");
            await Assert.That(stage).IsEqualTo(LadderStage.Shadow);
            await Assert.That(trials.NTrials).IsEqualTo(1);

            var (_, trials2) = VaultDb.Open(path);
            await Assert.That(trials2.NTrials).IsEqualTo(1);
            await Assert.That(trials2.Entries[0].ParamsJson).Contains("b8");

            // Sim table must not be able to request paper/real.
            await Assert.That(() => PromotionPolicy.RefuseSimOnlyPromotion(LadderStage.Paper))
                .Throws<InvalidOperationException>();
            await Assert.That(() => PromotionPolicy.RefuseSimOnlyPromotion(LadderStage.RealCandidate))
                .Throws<InvalidOperationException>();
        }
        finally
        {
            try { File.Delete(path); } catch { /* */ }
        }
    }

    [Test]
    public async Task Emit_Fail_StaysShadow()
    {
        var trials = new TrialLedger();
        var stage = TierAShadowVerdict.Emit(
            trials, "kalman", pass: false,
            paramsJson: "{\"family\":\"b8\"}",
            dateFrom: "2015", dateTo: "2022");
        await Assert.That(stage).IsEqualTo(LadderStage.Shadow);
        await Assert.That(trials.NTrials).IsEqualTo(1);
    }

    [Test]
    public async Task SoccerStrict_Fold_SameTickets_Smoke()
    {
        var db = ResolveDb();
        if (db is null)
        {
            Console.WriteLine("SKIP T4c soccer-strict smoke: no possiblymaybe.db");
            return;
        }

        var (v0, st) = await SoccerStrictTierA.RunFoldAsync(
            db, "MLS", "2023", "2025",
            MlsChampConfig.MarketWeight, MlsChampConfig.MinEv, MlsChampConfig.MinAbsTotalEdge,
            MlsChampConfig.EmaAlpha, useDixonColes: false, dcAlpha: 0.12);
        Console.WriteLine($"MLS 2023 V0 n={v0.Bets} clv={v0.MeanClv:F2}  strict n={st.Bets} clv={st.MeanClv:F2}");
        await Assert.That(v0.Bets).IsGreaterThanOrEqualTo(st.Bets); // strict ⊆ V0 edge-wise (stricter floor)
        await Assert.That(v0.GamesScored).IsEqualTo(st.GamesScored);
    }

    [Test]
    public async Task OregonExclusion_StillOn_NbaAndSoccer()
    {
        await Assert.That(OregonExclusion.IsExcluded("NBA", "Portland Trail Blazers", "BOS")).IsTrue();
        await Assert.That(OregonExclusion.IsExcluded("MLS", "Portland Timbers", "LAFC")).IsTrue();
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
