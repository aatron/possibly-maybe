using PossiblyMaybe.Cli.Variants;
using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Tests;

public class VariantsTests
{
    [Test]
    public async Task Registry_LaunchSet_ImmutableIds()
    {
        var ids = VariantRegistry.All.Select(v => v.Id).ToList();
        await Assert.That(ids).IsEquivalentTo([
            "V0", "V0L", "V1", "V2", "V3", "V4", "V5", "V-SA0",
            "V-POOL", "V-ROSTER", BlendFamily.InvVarId, "refs-nba",
            AvailabilityFamily.AbsenceId, AvailabilityFamily.InjuryGuardId,
            "nba-volume", "nba-volume-efg",
            "mls-dc", "+poisson-all", "+negbin", "+dc-rho",
            "consensus-outlier", "line-move-follow",
            "V-FC-NBA", "V-FC-MLS", "V-FC-LIGUE1", "V-FC-LIGAMX", "V-FC-SERIEA",
            "V-SOC-STRICT", "V-NBA-STRICT", "V-EV-04-10", "V-EV-10-20", "V-EV-04-20",
            "+conformal", "kalman",
        ]);
        await Assert.That(VariantRegistry.V0.Label).IsEqualTo("champ-2026-11-07");
        await Assert.That(VariantRegistry.V0.NbaOverProb).IsEqualTo(NbaOverProbKind.NormalSigma);
        await Assert.That(VariantRegistry.Require("V0L").NbaOverProb).IsEqualTo(NbaOverProbKind.Logistic);
        await Assert.That(VariantRegistry.Require("V1").EdgeCap).IsTrue();
        await Assert.That(VariantRegistry.Require("V2").LineMove).IsTrue();
        await Assert.That(VariantRegistry.Require("V5").UseRealJuice).IsTrue();
        await Assert.That(VariantRegistry.Require("V-SA0").Leagues!.Contains("SERIEA")).IsFalse();
        await Assert.That(VariantRegistry.Require("V-POOL").Pooling).IsTrue();
        await Assert.That(VariantRegistry.Require("V-ROSTER").RosterReset).IsTrue();
        await Assert.That(VariantRegistry.Require(BlendFamily.InvVarId).InvVarBlend).IsTrue();
        await Assert.That(VariantRegistry.Require("refs-nba").UseRefTotals).IsTrue();
        await Assert.That(VariantRegistry.Require("refs-nba").Leagues!.Contains("NBA")).IsTrue();
        await Assert.That(VariantRegistry.Require(AvailabilityFamily.AbsenceId).AbsenceAdjust).IsTrue();
        await Assert.That(VariantRegistry.Require(AvailabilityFamily.InjuryGuardId).InjuryGuard).IsTrue();
        await Assert.That(VariantRegistry.Require("nba-volume").MarketWeight).IsEqualTo(0.6);
        await Assert.That(VariantRegistry.Require("mls-dc").SoccerOverProb).IsEqualTo(SoccerOverProbKind.PoissonAll);
        await Assert.That(VariantRegistry.Require("consensus-outlier").ConsensusOutlier).IsTrue();
        await Assert.That(VariantRegistry.Require("V-FC-NBA").FittedCurve).IsTrue();
        await Assert.That(VariantRegistry.Require("+conformal").ConformalFilter).IsTrue();
        await Assert.That(VariantRegistry.Require("kalman").UseKalmanRatings).IsTrue();
    }

    [Test]
    public async Task Filter_RefsNba_AddsCrewResidual()
    {
        var baseCand = NbaCand(home: "BOS", away: "MIA", line: 220, model: 220, sigma: 12);
        var withRes = baseCand with { RefTotalsResidual = 12 };
        var v0 = VariantRegistry.Evaluate(VariantRegistry.V0, withRes);
        var refs = VariantRegistry.Evaluate(VariantRegistry.Require("refs-nba"), withRes);
        await Assert.That(v0.Pick).IsFalse(); // |edge| 0 under market blend
        await Assert.That(refs.Pick).IsTrue();
        await Assert.That(refs.Side).IsEqualTo("OVER");
    }

    [Test]
    public async Task Filter_RefsNba_SkipsNonNba()
    {
        var c = new SlateCandidate(
            "MLS", "20261107", "LAFC", "SEA", 2.8, 1.4, 1.4,
            2.5, 2.5, -110, -110, 19,
            ChampConfig.MarketWeight, 0.25, ChampConfig.MinEv, false,
            RefTotalsResidual: 1.0);
        var d = VariantRegistry.Evaluate(VariantRegistry.Require("refs-nba"), c);
        await Assert.That(d.Pick).IsFalse();
        await Assert.That(d.SkipReason).IsEqualTo(VariantSkip.LeagueOff);
    }

    [Test]
    public async Task Pooling_ShrinksOnOpeningPhase_NoopOtherwise()
    {
        var raw = NbaCand(home: "BOS", away: "MIA", line: 220, model: 240, sigma: 19)
            with { SeasonPhase = "opening-weeks" };
        var plain = NbaCand(home: "BOS", away: "MIA", line: 220, model: 240, sigma: 19)
            with { SeasonPhase = "playoffs" };

        var vPool = VariantRegistry.Require("V-POOL");
        var adj = PoolingFamily.Apply(vPool, raw);
        await Assert.That(adj.ModelExp).IsEqualTo(
            PoolingFamily.Shrink(240, PoolingFamily.LeagueMeanTotal("NBA"), PoolingFamily.EarlySeasonWeight));

        var dPool = VariantRegistry.Evaluate(vPool, raw);
        var dV0 = VariantRegistry.Evaluate(VariantRegistry.V0, raw);
        await Assert.That(dPool.AbsEdge).IsLessThan(dV0.AbsEdge!);

        var dPlayoffs = VariantRegistry.Evaluate(vPool, plain);
        var dV0Playoffs = VariantRegistry.Evaluate(VariantRegistry.V0, plain);
        await Assert.That(dPlayoffs.AbsEdge).IsEqualTo(dV0Playoffs.AbsEdge);
        await Assert.That(dPlayoffs.ModelProb).IsEqualTo(dV0Playoffs.ModelProb);
    }

    [Test]
    public async Task RosterReset_ShrinksAndWidensSigma_WhenFlagged()
    {
        var flagged = NbaCand(home: "BOS", away: "MIA", line: 220, model: 240, sigma: 16)
            with { RosterFlag = true };
        var clear = NbaCand(home: "BOS", away: "MIA", line: 220, model: 240, sigma: 16)
            with { RosterFlag = false };

        var v = VariantRegistry.Require("V-ROSTER");
        var adj = PoolingFamily.Apply(v, flagged);
        await Assert.That(adj.ModelExp).IsEqualTo(
            PoolingFamily.Shrink(240, PoolingFamily.LeagueMeanTotal("NBA"), PoolingFamily.RosterResetWeight));
        await Assert.That(adj.NbaSigma).IsEqualTo(16 * PoolingFamily.RosterSigmaMul);

        var dFlag = VariantRegistry.Evaluate(v, flagged);
        var dV0 = VariantRegistry.Evaluate(VariantRegistry.V0, flagged);
        await Assert.That(dFlag.AbsEdge).IsLessThan(dV0.AbsEdge!);

        var dClear = VariantRegistry.Evaluate(v, clear);
        var dV0Clear = VariantRegistry.Evaluate(VariantRegistry.V0, clear);
        await Assert.That(dClear.AbsEdge).IsEqualTo(dV0Clear.AbsEdge);
        await Assert.That(dClear.ModelProb).IsEqualTo(dV0Clear.ModelProb);
    }

    [Test]
    public async Task Evaluate_InvVarBlend_UsesLockedSigmas()
    {
        var c = NbaCand(home: "BOS", away: "MIA", line: 220, model: 232, sigma: 19);
        var v0 = VariantRegistry.Evaluate(VariantRegistry.V0, c);
        var inv = VariantRegistry.Evaluate(VariantRegistry.Require(BlendFamily.InvVarId), c);
        var w = MarketBlend.MarketWeightFromSigmas(BlendFamily.NbaSigmaModel, BlendFamily.NbaSigmaLine);
        var expectedEdge = Math.Abs(w * 220 + (1 - w) * 232 - 220);

        await Assert.That(v0.AbsEdge).IsEqualTo(6.0);
        await Assert.That(inv.AbsEdge).IsEqualTo(expectedEdge).Within(1e-9);
        await Assert.That(inv.AbsEdge).IsNotEqualTo(v0.AbsEdge);
        await Assert.That(inv.Side).IsEqualTo("OVER");
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
        // sigma 19 keeps EV under Guards.RecommendedEdgeCap (0.25); V3 must still pick.
        var c = NbaCand(home: "BOS", away: "MIA", line: 220, model: 232, sigma: 19);
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

    /// <summary>A1: V0 / V3 bit-identical post-split on a fixed fixture candidate.</summary>
    [Test]
    public async Task Evaluate_V0_V3_Fixture_StableAfterSplit()
    {
        var c = NbaCand(home: "BOS", away: "MIA", line: 220, model: 232, open: 220, sigma: 19);
        var v0 = VariantRegistry.Evaluate(VariantRegistry.V0, c);
        var v3 = VariantRegistry.Evaluate(VariantRegistry.Require("V3"), c);

        await Assert.That(v0.Pick).IsTrue();
        await Assert.That(v0.Side).IsEqualTo("OVER");
        await Assert.That(v0.AmericanOdds).IsEqualTo(-110);
        await Assert.That(v0.SkipReason).IsNull();
        await Assert.That(v0.AbsEdge).IsEqualTo(6.0); // (0.5*220 + 0.5*232) - 220

        await Assert.That(v3.Pick).IsTrue();
        await Assert.That(v3.Side).IsEqualTo("OVER");
        await Assert.That(v3.AmericanOdds).IsEqualTo(-110);
        await Assert.That(v3.Line).IsEqualTo(220);
        await Assert.That(v3.AbsEdge).IsEqualTo(v0.AbsEdge);
        await Assert.That(v3.ModelProb).IsEqualTo(v0.ModelProb);
        await Assert.That(v3.Ev).IsEqualTo(v0.Ev);
    }

    /// <summary>A1: V5 UseRealJuice reads DK american when present (not silent −110).</summary>
    [Test]
    public async Task Filter_V5_UsesRealDkJuice()
    {
        var c = NbaCand(home: "BOS", away: "MIA", line: 220, model: 232, sigma: 19,
            dkOver: -105, dkUnder: -115);
        var v3 = VariantRegistry.Evaluate(VariantRegistry.Require("V3"), c);
        var v5 = VariantRegistry.Evaluate(VariantRegistry.Require("V5"), c);

        await Assert.That(v3.Pick).IsTrue();
        await Assert.That(v3.Side).IsEqualTo("OVER");
        await Assert.That(v3.AmericanOdds).IsEqualTo(-110);

        await Assert.That(v5.Pick).IsTrue();
        await Assert.That(v5.Side).IsEqualTo("OVER");
        await Assert.That(v5.AmericanOdds).IsEqualTo(-105);
        await Assert.That(v5.Ev).IsGreaterThan(v3.Ev); // better price than −110
    }

    [Test]
    public async Task CandidateStore_RoundTrip()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pm-cand-{Guid.NewGuid():N}.db");
        try
        {
            var c = NbaCand(home: "BOS", away: "MIA", line: 220, model: 232, sigma: 15,
                dkOver: -102, dkUnder: -118);
            var n = await VariantEvaluate.RecordAsync(path, [c]);
            await Assert.That(n).IsEqualTo(VariantRegistry.All.Count);

            var (db, _) = await VariantLedger.OpenAsync(path);
            await using (db)
            {
                var cand = db.CandidateEntries.Single();
                await Assert.That(cand.Sport).IsEqualTo("NBA");
                await Assert.That(cand.GameKey).IsEqualTo("20261107 MIA@BOS");
                await Assert.That(cand.ModelExp).IsEqualTo(232);
                await Assert.That(cand.DkOverAmerican).IsEqualTo(-102);
                await Assert.That(cand.DkUnderAmerican).IsEqualTo(-118);

                var slate = VariantLedger.ToSlate(cand);
                var again = VariantRegistry.Evaluate(VariantRegistry.Require("V5"), slate);
                await Assert.That(again.AmericanOdds).IsEqualTo(-102);

                var shadow = db.ShadowEntries.Single(r => r.VariantId == "V5");
                await Assert.That(shadow.AmericanOdds).IsEqualTo(-102);
            }

            // Re-eval from store alone (no new model inputs).
            var n2 = await VariantEvaluate.ReEvalStoredAsync(path);
            await Assert.That(n2).IsEqualTo(VariantRegistry.All.Count);
        }
        finally
        {
            try { File.Delete(path); } catch { /* */ }
        }
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
                await Assert.That(db.CandidateEntries.Count()).IsEqualTo(1);
            }
        }
        finally
        {
            try { File.Delete(path); } catch { /* */ }
        }
    }



    [Test]
    public async Task Filter_NbaVolume_LooserEdge_PicksWhenV0Skips()
    {
        // Δmodel=10 → V0 edge=5 (<6 NO_EDGE); volume edge=4 (≥3.5 pick).
        var c = NbaCand(home: "BOS", away: "MIA", line: 220, model: 230, sigma: 19);
        var v0 = VariantRegistry.Evaluate(VariantRegistry.V0, c);
        var vol = VariantRegistry.Evaluate(VariantRegistry.Require("nba-volume"), c);
        await Assert.That(v0.Pick).IsFalse();
        await Assert.That(v0.SkipReason).IsEqualTo(VariantSkip.NoEdge);
        await Assert.That(vol.Pick).IsTrue();
        await Assert.That(vol.AbsEdge).IsEqualTo(4.0);
    }


    [Test]
    public async Task Filter_NbaVolume_Soccer_LeagueOff()
    {
        var c = new SlateCandidate(
            "MLS", "20261107", "LAFC", "SEA",
            2.8, 1.5, 1.3, 2.5, 2.5, -110, -110, 0,
            0.4, 0.35, 0.04, false);
        var d = VariantRegistry.Evaluate(VariantRegistry.Require("nba-volume"), c);
        await Assert.That(d.SkipReason).IsEqualTo(VariantSkip.LeagueOff);
    }


    [Test]
    public async Task Filter_NbaVolumeEfg_UsesModelExpEfg()
    {
        var c = NbaCand(home: "BOS", away: "MIA", line: 220, model: 230, sigma: 19)
            with { ModelExpEfg = 240 };
        var vol = VariantRegistry.Evaluate(VariantRegistry.Require("nba-volume"), c);
        var efg = VariantRegistry.Evaluate(VariantRegistry.Require("nba-volume-efg"), c);
        await Assert.That(vol.AbsEdge).IsEqualTo(4.0);
        // w=0.6: 0.6*220+0.4*240 - 220 = 8
        await Assert.That(efg.AbsEdge).IsEqualTo(8.0);
        await Assert.That(efg.Pick).IsTrue();
    }


    [Test]
    public async Task Filter_MlsDc_Skips_NonMls()
    {
        var c = SoccerCand("LIGUE1", "PSG", "OM", 2.5, 1.4, 1.3);
        var d = VariantRegistry.Evaluate(VariantRegistry.Require("mls-dc"), c);
        await Assert.That(d.SkipReason).IsEqualTo(VariantSkip.LeagueOff);
    }


    [Test]
    public async Task SoccerDist_Curves_Differ_On_MlsLogisticLeague()
    {
        // Edge ≥ MLS min; line 1.5 so DC-ρ moves P(over) (at 2.5 ρ cancels on totals).
        var c = SoccerCand("MLS", "LAFC", "SEA", 1.5, 1.8, 1.5);
        var v0 = VariantRegistry.Evaluate(VariantRegistry.V0, c);
        var pois = VariantRegistry.Evaluate(VariantRegistry.Require("+poisson-all"), c);
        var nb = VariantRegistry.Evaluate(VariantRegistry.Require("+negbin"), c);
        var rho = VariantRegistry.Evaluate(VariantRegistry.Require("+dc-rho"), c);
        var mlsDc = VariantRegistry.Evaluate(VariantRegistry.Require("mls-dc"), c);

        await Assert.That(v0.SkipReason).IsNotEqualTo(VariantSkip.NoEdge);
        await Assert.That(pois.ModelProb).IsNotEqualTo(v0.ModelProb);
        await Assert.That(mlsDc.ModelProb).IsEqualTo(pois.ModelProb);
        await Assert.That(nb.ModelProb).IsNotEqualTo(pois.ModelProb);
        await Assert.That(rho.ModelProb).IsNotEqualTo(pois.ModelProb);
    }


    [Test]
    public async Task Kaunitz_IsSoftOutlier_WhenMaxOddsBeatConsensus()
    {
        // Three books ~1.91 mean; one soft at 2.30 → outlier at α=0.05.
        await Assert.That(MarketStructureFamily.IsSoftOutlier([1.87, 1.91, 2.30], 0.05)).IsTrue();
        // Tight consensus, no soft book.
        await Assert.That(MarketStructureFamily.IsSoftOutlier([1.90, 1.91, 1.92], 0.05)).IsFalse();
        await Assert.That(MarketStructureFamily.IsSoftOutlier([1.91], 0.05)).IsFalse();
    }


    [Test]
    public async Task Filter_ConsensusOutlier_NeedsVenueAsks_ThenSoftGate()
    {
        // Model OVER; DK alone → NO_VENUE_ASKS; soft Kalshi ask → PICK; tight Kalshi → NO_OUTLIER.
        var baseC = NbaCand(home: "BOS", away: "MIA", line: 220, model: 232, open: 220, sigma: 19,
            dkOver: -110, dkUnder: -110);
        var v = VariantRegistry.Require("consensus-outlier");
        var alone = VariantRegistry.Evaluate(v, baseC);
        await Assert.That(alone.Pick).IsFalse();
        await Assert.That(alone.SkipReason).IsEqualTo(VariantSkip.NoVenueAsks);

        var soft = baseC with { KalshiOverAsk = 0.38 }; // Ω≈2.63 vs DK ~1.91
        var softD = VariantRegistry.Evaluate(v, soft);
        await Assert.That(softD.Pick).IsTrue();
        await Assert.That(softD.Side).IsEqualTo("OVER");

        var tight = baseC with { KalshiOverAsk = 0.52 }; // Ω≈1.92
        var tightD = VariantRegistry.Evaluate(v, tight);
        await Assert.That(tightD.Pick).IsFalse();
        await Assert.That(tightD.SkipReason).IsEqualTo(VariantSkip.NoOutlier);
    }


    [Test]
    public async Task Filter_LineMoveFollow_RequiresMoveToward()
    {
        var v = VariantRegistry.Require("line-move-follow");
        // OVER: need line up ≥ NBA k=2.0
        var flat = NbaCand(home: "BOS", away: "MIA", line: 220, model: 232, open: 220, sigma: 19);
        var flatD = VariantRegistry.Evaluate(v, flat);
        await Assert.That(flatD.Pick).IsFalse();
        await Assert.That(flatD.SkipReason).IsEqualTo(VariantSkip.NoFollow);

        // open→current +2 (≥ NBA k) and blended edge still ≥ MinAbsTotalEdge (6).
        var toward = NbaCand(home: "BOS", away: "MIA", line: 222, model: 236, open: 220, sigma: 19);
        var towardD = VariantRegistry.Evaluate(v, toward);
        await Assert.That(towardD.Pick).IsTrue();
        await Assert.That(towardD.Side).IsEqualTo("OVER");

        var against = NbaCand(home: "BOS", away: "MIA", line: 217, model: 232, open: 220, sigma: 19);
        var againstD = VariantRegistry.Evaluate(v, against);
        await Assert.That(againstD.Pick).IsFalse();
        await Assert.That(againstD.SkipReason).IsEqualTo(VariantSkip.NoFollow);
    }


    [Test]
    public async Task Filter_FittedCurve_UsesLockedSigma_NotCandidateSigma()
    {
        // Candidate NbaSigma=10 is overconfident vs locked 22.8; fitted P closer to 0.5.
        var c = NbaCand(home: "BOS", away: "MIA", line: 220, model: 232, sigma: 10);
        var v0 = VariantRegistry.Evaluate(VariantRegistry.V0, c);
        var fc = VariantRegistry.Evaluate(VariantRegistry.Require("V-FC-NBA"), c);
        await Assert.That(v0.Pick).IsTrue();
        await Assert.That(fc.Pick).IsTrue();
        await Assert.That(fc.ModelProb).IsLessThan(v0.ModelProb);
        await Assert.That(fc.ModelProb).IsEqualTo(
            TotalsMath.NormalOverProb(226, 220, FittedCurves.NbaSigma)).Within(1e-12);
    }


    [Test]
    public async Task Filter_SoccerStrict_RequiresHalfGoalEdge_AndEvBand()
    {
        // Champ MLS edge floor 0.25; blended edge = 0.4*(2.5) + 0.6*model wait — use marketWeight 0.4.
        // Abs edge after blend: |0.4*2.5 + 0.6*3.2 - 2.5| = |1.0+1.92-2.5|=0.42 → below 0.5 → NO_EDGE.
        var thin = SoccerCand(home: "LAFC", away: "RSL", line: 2.5, model: 3.2, minEdge: 0.25);
        var strict = VariantRegistry.Require("V-SOC-STRICT");
        var dThin = VariantRegistry.Evaluate(strict, thin);
        await Assert.That(dThin.Pick).IsFalse();
        await Assert.That(dThin.SkipReason).IsEqualTo(VariantSkip.NoEdge);

        // Larger model → edge ≥ 0.5; V0 (candidate floor 0.25) and strict both can pick.
        var fat = SoccerCand(home: "LAFC", away: "RSL", line: 2.5, model: 3.6, minEdge: 0.25);
        var dFat = VariantRegistry.Evaluate(strict, fat);
        var dV0 = VariantRegistry.Evaluate(VariantRegistry.V0, thin);
        await Assert.That(dV0.SkipReason).IsNotEqualTo(VariantSkip.NoEdge); // 0.42 ≥ 0.25
        await Assert.That(strict.EdgeCapEv).IsEqualTo(0.20);
        await Assert.That(strict.MinEv).IsEqualTo(0.04);
        // Fat edge: |0.4*2.5 + 0.6*3.6 - 2.5| = 0.66 ≥ 0.5
        await Assert.That(dFat.AbsEdge).IsGreaterThanOrEqualTo(0.5);
    }


    [Test]
    public async Task Filter_NbaStrict_RaisesEdgeFloor()
    {
        // Champ floor 6; blended edge = 0.5*(220+232)-220 = 6 → below nba-strict 8.
        var c = NbaCand(home: "BOS", away: "MIA", line: 220, model: 232, sigma: 19);
        var v0 = VariantRegistry.Evaluate(VariantRegistry.V0, c);
        var st = VariantRegistry.Evaluate(VariantRegistry.Require("V-NBA-STRICT"), c);
        await Assert.That(v0.Pick).IsTrue();
        await Assert.That(st.Pick).IsFalse();
        await Assert.That(st.SkipReason).IsEqualTo(VariantSkip.NoEdge);
    }


    [Test]
    public async Task Filter_EvBand_10_20_SkipsLowEv()
    {
        // Edge clears champ floor (blended +6); wide σ keeps EV ~5% → below 10% band floor.
        var c = NbaCand(home: "BOS", away: "MIA", line: 220, model: 232, sigma: 50);
        var band = VariantRegistry.Evaluate(VariantRegistry.Require("V-EV-10-20"), c);
        var wide = VariantRegistry.Evaluate(VariantRegistry.Require("V-EV-04-20"), c);
        await Assert.That(band.AbsEdge).IsEqualTo(6.0);
        await Assert.That(band.SkipReason).IsEqualTo(VariantSkip.LowEv);
        await Assert.That(band.Ev).IsLessThan(0.10);
        await Assert.That(wide.Pick).IsTrue(); // still ≥ 4%
    }
    private static SlateCandidate NbaCand(
        string home, string away, double line, double model,
        double? open = null, double sigma = 19.0,
        int? dkOver = -110, int? dkUnder = -110)
        => new("NBA", "20261107", home, away, model, null, null,
            line, open ?? line, dkOver, dkUnder, sigma,
            ChampConfig.MarketWeight, ChampConfig.MinAbsTotalEdge, ChampConfig.MinEv, false);

    private static SlateCandidate SoccerCand(
        string sport, string home, string away, double line, double lh, double la)
        => new(sport, "20261107", home, away, lh + la, lh, la,
            line, line, -110, -110, 0,
            MlsChampConfig.MarketWeight, MlsChampConfig.MinAbsTotalEdge, MlsChampConfig.MinEv, false);

    private static SlateCandidate SoccerCand(
        string home, string away, double line, double model, double minEdge)
        => new("MLS", "20261107", home, away, model, model / 2, model / 2,
            line, line, -110, -110, 0,
            MlsChampConfig.MarketWeight, minEdge, MlsChampConfig.MinEv, false);
}
