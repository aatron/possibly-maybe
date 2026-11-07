# Phase 4b — MLB soft-spot hunt

**Date:** 2026-10-29  
**Data:** D1 flancast append via `import-round2-odds` — **25,571** games (2011–2021) + existing 2025. Total MLB games ≈ 27,861.

## Protocol

- Model: existing `MlbTotalsBacktest` (SP FIP × park when present; else runs EMA).
- Holdout locked: tipoff ≥ **2025-09-01** (prior FAIL holdout — burned; report only).
- Grid: w∈{0.4…0.7} × edge∈{0.5…1.5} → **N_trials=20**. AssumedJuice −110.

## Result (selection = max train PnL — known overfit risk)

| | Train (champ) | Hold Sep–2025 |
|---|---|---|
| Config | w=0.50 edge=1.50 | |
| n | 802 | 18 |
| mean CLV | 0.17 | 0.14 |
| PnL | +92.2 | **−0.8** |

Train looks rich across the grid (thousands of bets, +PnL) but **hold n=18** and selected champ loses. Other cells with hold PnL>0 have tiny n (e.g. w=0.70 edge=1.00 hold n=13).

**Verdict: FAIL** under current holdout gate. Data gap filled; money path still not proven OOS.

## Justification / next (not run)

Prior FAIL was data-limited; multi-season O→C is now in the warehouse. A proper Tier A walk-forward (2011–2019 folds) + look-once **2021** Tier C was **not** run this pass (holdout already burned for 2025). Do not raise live MLB stakes. Optional follow-up: Tier A on pre-2021 only, then score 2021 once.
