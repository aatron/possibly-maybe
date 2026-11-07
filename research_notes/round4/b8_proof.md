# B8 proof: +conformal, kalman

**Landed on main** via unified VariantCore (worktree `../pm-r4-b8` mid-flight; not `--no-ff` merged).

## What shipped

1. `HigherRiskFamily.cs` — ids `+conformal` (ConformalFilter) and `kalman` (UseKalmanRatings).
2. Evaluate gates: conformal skips when line inside abs-residual half-width; kalman uses `SlateCandidate.KalmanModelExp` when set.
3. Helpers: `ConformalAbsResidual`, `LocalLevelKalman`, `PaceEfficiencyKalmanBook` (steady gain ≈ champ α=0.15).
4. CLI: `tier-a-higher-risk` (`HigherRiskTierA`) — pre-declared keep = mean fold CLV ≥ V0 and n≥40.

## Unit proof (2026-11-07)

`HigherRiskTests` + registry: conformal abstains inside interval / picks outside; kalman flips side when KalmanModelExp diverges; PaceKalman diverges from EMA after one update; steady gain matches 0.15.

## Tier A

CLI ready (`tier-a-b8` / Round 5 `tier-a-t4c`). **Fold table scored once** on 2026-11-07 — see [`../round5/t4c_proof.md`](../round5/t4c_proof.md):

| Variant | Verdict |
|---|---|
| `+conformal` | FAIL → Shadow marked FAIL (n=0) |
| `kalman` | PASS → Shadow (mean CLV = V0, n=233) |

No paper promotion from the table. Do not re-tune.

## N_trials

Family = 2 (+conformal, kalman). Charged in durable TrialLedger during T4c.
