# Phase 4b — NFL soft-spot hunt

**Date:** 2026-10-29  
**Data:** D1 flancast O→C overlays via `import-round2-odds` (~2,892 games with real open≠close, 2011–2021).

## Protocol

- Model: existing `NflTotalsBacktest` (EPA + roof/wind, AssumedJuice −110).
- Bets **require** OpenClose (`requireOpenClose=true`) — no CloseOnly mirage CLV.
- Holdout locked: season **2024** (prior FAIL holdout; not a fresh Tier C).
- Grid: w∈{0.5,0.6,0.7,0.85} × edge∈{2…6} → **N_trials=20**.

## Result

| | Train (champ) | Hold 2024 |
|---|---|---|
| Config | w=0.50 edge=2.0 | |
| n | 480 | 57 |
| mean CLV | 0.51 | 0.48 |
| PnL | +2.5 | **−1.6** |

**Verdict: FAIL** — still not money on holdout after filling the data gap. Prior FAIL was partly data-limited; with multi-season O→C the EPA+dome/wind blend still loses dollars OOS.

## Justification

NFL totals market looks efficient at our edge gates once CLV is real. Do **not** promote an NFL ChampConfig. Optional later: Poisson/Normal `OverProb` sizing-only (same tickets) — only if Kelly-lane PnL lifts without expanding the grid.
