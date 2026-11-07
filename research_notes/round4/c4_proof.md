# C4 proof: MLB redo (Tier A 2011–2019 + look-once 2021)

**Branch:** `r4/c4`  
**Date:** 2026-11-07  
**Worktree:** `../pm-r4-c4`

## Holdout lock (before any tuning)

| Lock | Value | Status |
|---|---|---|
| Tier C look-once season | **2021** | Locked in this proof **before** fold results / gate choice |
| Tier A folds | 2011, 2012, 2013, 2014, 2015, 2016, 2017, 2018, 2019 | Pre-registered |
| Skip | 2020 (COVID short season — warm-up only, not a fold) | Pre-registered |
| Burned prior holdout | 2025-09-01 date cut (r2 hunt) | Report-only forever; not used for C4 selection |
| Gates | `MlbChampConfig`: w=0.50, \|edge\|≥1.0, EV≥4%, α=0.12 | Locked here; **N_trials=1** (no grid on Tier A / Tier C) |

2021 was **not** used to choose w / edge / EV. Tier C is look-once after Tier A.

## What landed

| Piece | Path |
|---|---|
| Champ gates | `PossiblyMaybe.Domain/MlbChampConfig.cs` |
| Pack (C0 shape) | `PossiblyMaybe.Domain/SportPacks/MlbSportPack.cs` |
| Registry | `SportPackRegistry` → `MLB` |
| Tier A / C runner | `PossiblyMaybe.Cli/Backtest/MlbTotalsTierA.cs` |
| Season fold | `MlbTotalsBacktest.RunSeasonFoldAsync` |
| CLI | `tier-a-mlb [dbPath]` |
| Spring F5 / team-totals | Pack market ids + `SpringSubMarketsReady`; models not fitted |
| Test | `SportPackTests.Mlb_Pack_Locks_Holdout_And_Spring_Ready` |

## Spring ready flag (F5 / team totals)

| Flag | Value | Meaning |
|---|---|---|
| `MlbSportPack.SpringSubMarketsReady` | **true** | Pack declares market ids + Kalshi series for spring shadow |
| `F5Market` / `TeamTotalMarket` | `F5_TOTAL` / `TEAM_TOTAL` | Candidate-store market labels |
| `F5KalshiSeries` / `TeamTotalKalshiSeries` | `KXMLBF5TOTAL` / `KXMLBTEAMTOTAL` | Tracker series (from r2 line_tracker notes) |
| `F5ModelReady` / `TeamTotalsModelReady` | **false** | No SP/park F5 or team-total model yet — scaffold only |

## Tier A results

Command: `dotnet run --project PossiblyMaybe.Cli -c Release -- tier-a-mlb <main possiblymaybe.db>`  
Model: SP-FIP × park when pitching row present; else runs EMA (α=0.12) × park. Assumed juice −110.

| fold | n | mean CLV | PnL | hit |
|---|---|---|---|---|
| 2011 | 140 | −0.02 | +7.4 | 52.1% |
| 2012 | 171 | −0.01 | −10.9 | 44.4% |
| 2013 | 134 | +0.06 | +10.5 | 53.7% |
| 2014 | 150 | +0.04 | +26.8 | 60.0% |
| 2015 | 240 | +0.08 | +48.8 | 60.4% |
| 2016 | 264 | +0.03 | +22.7 | 55.3% |
| 2017 | 259 | +0.09 | +54.5 | 61.8% |
| 2018 | 234 | +0.43 | −3.3 | 48.3% |
| 2019 | 261 | +0.36 | +10.5 | 52.9% |
| **aggregate** | **1853** | **mean fold CLV +0.118** | **sum +166.9u** | PnL>0 folds **7/9** |

**Tier A verdict: PASS** — mean CLV>0, majority folds PnL>0 (7/9), n≥40.

## Tier C look-once (2021)

| | n | mean CLV | PnL | hit |
|---|---|---|---|---|
| 2021 | 387 | +0.26 | +34.9 | 54.3% |

**Tier C verdict: PASS** (look-once report only — never used for selection).

## Data notes

- Main DB has MLB O→C for 2011–2025 (flancast + later imports).
- 2021 game-row count is inflated (~2×) vs a normal season (overlapping imports); edge filter yields n=387 bets (still treat as mildly upper-biased vs a deduped season).
- SP FIP pitching rows cover mostly 2025; historical folds use runs EMA + park factor when present.
- Park factors loaded from `data/raw/mlb/park_factors.csv` when available (main checkout).

## Oregon

Pack lists Hillsboro Hops / Eugene Emeralds / Portland Pickles (already in `OregonExclusion` names). No MLB franchise in Oregon; alias hook = `SportTeams.TryMlb`.

## Risks

- Historical model is mostly runs-EMA (not SP FIP) — Gate 1 SP path still thin outside 2025.
- Assumed −110 juice; real DK prices may shrink EV.
- 2021 duplicates can bias look-once n/CLV; dedupe before any promotion.
- F5 / team-totals are pack-ready only — no model, no Tier A.
- Do not raise live MLB stakes from this alone; shadow / paper only until radar + true close path land.
