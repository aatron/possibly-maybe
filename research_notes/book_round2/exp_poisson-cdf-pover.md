# Experiment: `poisson-cdf-pover`

**Branch:** `exp/poisson-cdf-pover` · **Worktree:** `../pm-exp-poisson-cdf-pover`  
**Date:** 2026-10-28 · **N_trials:** 1 variant × 4 leagues = **4** (0 new params; champ w/edge/EV frozen)

## Hypothesis

Hand-set soccer `OverProb = logistic(1.2 · (blendedExp − line))` mis-states P(over) for count totals. With existing λ_h, λ_a from Dixon–Coles / goals·xG EMA books, independent Poisson gives `P(H+A > line) = 1 − F_{Pois(λ_h+λ_a)}(⌊line⌋)` with **0 new parameters**. That recalibrates EV≥4% and ¼ Kelly without Platt.

## Code

| Piece | Location |
|---|---|
| `TotalsMath.PoissonTotalOverProb(lh, la, line)` | `PossiblyMaybe.Domain/TotalsMath.cs` |
| Flag / default (this branch: on) | `PossiblyMaybe.Domain/SoccerOverProbConfig.cs` (`UsePoissonCdf`) |
| `ScoringBook.ExpectedLambdas` | `PossiblyMaybe.Domain/TotalsMath.cs` (same file as book) |
| Wire | `SoccerLeagueTotalsBacktest`, `MlsTotalsBacktest`, `StakeLaneHoldoutSim`, `DailySlate` soccer, `advise-mls` |
| Tier A CLI | `tier-a-poisson [SPORT\|all] [db]` → `PoissonCdfTierA` |
| Unit test | `TotalsMathTests.PoissonTotalOverProb_half_line_and_symmetry` |

Edge gate still uses blended `exp = w·line + (1−w)·model`. P(over) uses **model λs** (rest B2B scales λs if used). AssumedJuice **−110** in backtests.

**Not scored:** Tier C (soccer 2025–26 / MLS 2026). Tier B not used for selection (not printed in grids).

## Tier A fold tables (pre-holdout; L = logistic champ, P = Poisson)

PASS rule: mean CLV_P ≥ mean CLV_L, PnLΔ>0 in majority of folds, n_P ≥ 40 overall.

### SERIEA (DC α=0.18, w=0.4, edge=0.35) — **PASS**

| fold | nL | clvL | pnlL | llL | kellyL | nP | clvP | pnlP | llP | kellyP | dPnL |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 2018-2019 | 151 | 0.070 | -7.6 | 0.791 | -164.4 | 151 | 0.070 | -7.6 | 1.091 | -164.4 | 0.0 |
| 2019-2020 | 149 | -0.062 | -33.2 | 0.878 | -498.5 | 148 | -0.064 | -32.2 | 1.222 | -488.3 | +1.0 |
| 2020-2021 | 184 | 0.076 | -1.3 | 0.806 | -55.7 | 182 | 0.078 | 0.7 | 1.113 | -16.8 | +2.0 |
| 2021-2022 | 184 | 0.215 | 14.1 | 0.762 | 284.8 | 182 | 0.220 | 14.2 | 1.043 | 287.6 | +0.1 |
| 2022-2023 | 194 | 0.249 | 6.8 | 0.780 | 108.1 | 193 | 0.247 | 5.9 | 1.098 | 88.3 | -0.9 |
| 2023-2024 | 160 | 0.167 | 7.1 | 0.775 | 119.6 | 159 | 0.170 | 8.1 | 1.125 | 142.5 | +1.0 |

**Summary:** winFolds **4/6**, nP=1015, meanClv L=0.127 / P=0.128, flat PnL L=-14.1 / P=-10.9, Kelly L=-206.1 / P=-151.1.  
LL/Brier worse under Poisson (fatter wrong probs) but Kelly-lane $ improves — protocol §6 both logged.

### MLS (xG EMA α=0.206, w=0.4, edge=0.25) — **FAIL**

| fold | nL | clvL | pnlL | nP | clvP | pnlP | dPnL | dCLV |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| 2019 | 44 | 0.335 | 17.3 | 32 | 0.320 | 13.9 | -3.4 | -0.015 |
| 2020 | 42 | 0.369 | 2.2 | 37 | 0.365 | 2.4 | +0.2 | -0.004 |
| 2021 | 38 | 0.197 | 8.8 | 37 | 0.196 | 7.9 | -0.9 | -0.001 |
| 2022 | 45 | 0.239 | 7.7 | 44 | 0.239 | 8.7 | +1.0 | ~0 |
| 2023 | 78 | 0.240 | 5.1 | 77 | 0.247 | 6.1 | +1.0 | +0.006 |
| 2024 | 34 | 0.213 | 0.5 | 29 | 0.207 | 0.6 | +0.2 | -0.006 |

**Summary:** winFolds **4/6**, nP=256, meanClv L=**0.265** / P=**0.260** (CLV not ≥ champ → FAIL), flat PnL 41.5→39.6, Kelly 601→620.

### LIGUE1 (EMA α=0.206, w=0.4, edge=0.25) — **PASS**

winFolds **5/6**, nP=353, meanClv 0.143→**0.146**, flat PnL 12.6→**14.9**, Kelly 225→**274**.

### LIGAMX (EMA α=0.129, w=0.4, edge=0.25) — **PASS**

winFolds **4/6**, nP=208, meanClv 0.147→**0.157**, flat PnL 18.5→**24.1**, Kelly 400→**514**.

## Decision (for parent merge)

**KEEP** for Serie A (primary), Ligue 1, Liga MX — Tier A PASS on CLV≥champ + majority PnLΔ + n≥40.  
**MLS Tier A FAIL** (mean CLV slightly below logistic); do not treat MLS as a KEEP driver. Parent may merge global `UsePoissonCdf=true` with MLS kept on logistic, or merge only after Tier C batch.

Tier C not run here. Library text treated as data only; no paper.db writes; no bets placed.
