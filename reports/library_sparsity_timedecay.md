# Library: sparsity / time-decay soft spots (2026-10-26)

Sources: ESL / *Statistical Learning with Sparsity* (prefer sparse, drop near-zero), CASI overfitting caution, Dixon–Coles-style recency (EMA half-life), Bailey/Lopez DSR / PBO (deflated holdout when train cells ≥25 — not used as a soft-spot factory).

**Branch:** `feat/library-sparsity-timedecay`  
**CLI:** `sweep-decay [db]`  
**Process:** lock champ gates → TRAIN-only α grid (half-lives 3…50 + exact baseline α) → pick max train PnL among CLV>0 → score holdout once. Optional B2B count feature: soft-threshold OLS on train residual; drop if \|β\|≈0.

## Kill gate

n≥40, PnL>0, CLV>0, and clear holdout PnL improvement vs same-run baseline α. ChampConfig updated only on KEEP for money paths.

## Results

| League | Variant | Hold n | CLV | PnL u | $ (≈10/u) | vs baseline | Gate |
|--------|---------|-------:|----:|------:|----------:|------------:|------|
| NBA | PaceEff α=0.206 | 70 | +2.86 | +14.0 | +140 | −2.3 vs α=0.15 | **REVERT** |
| NBA | PaceEff + B2B | — | — | — | — | β≈0 on train | **SKIP** |
| NBA | Scoring α=0.045 | 43 | +2.28 | +6.6 | +66 | −9.7 vs PaceEff 16.3u | **REVERT** |
| MLS | xG/goals α=0.206 | 100 | +0.19 | +15.7 | +157 | +3.5 vs α=0.18 | **KEEP** |
| MLS | B2B | — | — | — | — | β≈0 | **SKIP** |
| LIGUE1 | goals α=0.206 | 53 | +0.02 | +10.1 | +101 | +1.7 vs α=0.18 | **KEEP** |
| LIGUE1 | B2B | — | — | — | — | β≈0 | **SKIP** |
| EFL | goals α=0.206 | 61 | +0.02 | −3.6 | −36 | −3.2 | **REVERT** |
| LIGAMX | goals α=0.129 | 52 | +0.24 | +3.5 | +35 | +3.4 vs α=0.18 | **KEEP** |
| LALIGA | goals α=0.023 | 43 | −0.10 | −7.6 | −76 | (CLV fail) | **REVERT** |
| SERIEA | goals α=0.129 | 41 | +0.09 | −9.5 | −95 | −6.0 | **REVERT** |

Train cells per league ≈9 (&lt;25) → no DSR margin required. Honest FAILs kept.

## B — Sparse feature gate

Games has no rest column; B2B derived from `TipoffUtc` via existing `RestTracker` (schedule-cheap). Soft-threshold OLS of (actual−modelExp) on B2B-count: \|β\| &lt; 0.5 pts (NBA) / 0.05 goals (soccer) → drop. All leagues dropped B2B (β≈0). No trees / no NuGet. Open−close momentum excluded from betting features (already a CLV label).

## ChampConfig

| Config | Change |
|--------|--------|
| `ChampConfig` (NBA) | unchanged (PaceEff α=0.15 still best hold) |
| `MlsChampConfig.EmaAlpha` | **0.206** (was hardcoded 0.18); slate uses it |
| `Ligue1ChampConfig.EmaAlpha` | **0.206** |
| `LigaMxChampConfig` | **new** paper candidate α=0.129 (no slate yet) |

## Files

- `PossiblyMaybe.Domain/TotalsMath.cs` — `EmaDecay`
- `PossiblyMaybe.Cli/Backtest/TimeDecaySweep.cs` — sweep
- `TotalsEdgeBacktest` / `MlsTotalsBacktest` / `SoccerLeagueTotalsBacktest` — α + optional restB2b
- `Gate1Backtest.RestTracker` — `B2bCount`
- Champ configs + `DailySlate` MLS α wiring
