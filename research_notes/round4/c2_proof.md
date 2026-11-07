# C2 proof: NCAAF totals (SBRO 2007–22)

**Branch:** `r4/c2`  
**Date:** 2026-11-07  
**Agents:** Auto / Composer only

## Tier C lock (before any tuning)

| Field | Value | Why |
|---|---|---|
| Train max | `2021-22` | Last SBRO season used for gate selection / Tier A folds |
| **Tier C holdout** | **`2022-23`** | Newest full SBRO season on disk; locked **before** train sweep |
| Skip | `2023-24`+ | No on-disk O→C yet (CFBD/Odds API later); never tune on these |

Holdout is encoded in `NcaafSportPack.TierCHoldoutSeason` and asserted in `SportPackTests.Ncaaf_Pack_Locks_TierC_And_Oregon`. Train-only grid may print holdout numbers for the look-once report; selection uses train CLV/PnL only.

## What landed

| Piece | Path |
|---|---|
| Champ consts | `PossiblyMaybe.Domain/NcaafChampConfig.cs` |
| Pack | `PossiblyMaybe.Domain/SportPacks/NcaafSportPack.cs` |
| Registry | `SportPackRegistry.Ncaaf` |
| Import | existing `DiskOddsImporter` → `data/raw/round3_odds/sbro_ncaaf` (16 xlsx; W15) |
| Backtest | `PossiblyMaybe.Cli/Backtest/NcaafTotalsBacktest.cs` |
| CLI | `sweep-ncaaf [db]` (falls back to `../possibly-maybe/possiblymaybe.db`) |
| Oregon | SBRO football spellings + NcaaCode → exclusion codes; tests in `OregonExclusionTests` |

## Model

- Book: `ScoringBook` EMA (α=0.15, leagueInit=55), juice assumed −110.
- Require true Open+Close (`ClvQuality=OpenClose`).
- Skip any game with an Oregon-based side (`OregonExclusion` on NCAAF).
- Shadow policy: keep emitting candidates through bowls even when SBRO 2017/2022 files stop mid-December.

### Train-only selection → Tier C look-once (2026-11-07)

Grid: w ∈ {0.5,0.6,0.7,0.85} × edge ∈ {3,4,5,6}; pick max train PnL with train CLV>0 and n∈[80,4000].

| Slice | w | edge | n | mean CLV (pts) | hit | PnL (u) |
|---|---|---|---|---|---|---|
| Train (≤2021-22) | **0.60** | **3.0** | 1673 | +9.26 | 76.4% | +783.7 |
| **Tier C 2022-23** | 0.60 | 3.0 | 94 | +1.68 | 61.7% | +18.7 |

Verdict printed: `HOLD PASS — NCAAF totals (shadow candidate).`  
**Caveat:** several early Tier A folds show 90%+ hit rates at fixed provisional gates — treat as possible cold-start / SBRO artifact; do not promote to paper on this alone.

## Oregon / Oregon State football names

SBRO raw strings observed: `Oregon`, `OregonState`, `PortlandSt`.  
Mapped via `SportTeams.NcaaCode` → `ore` / `orst` / `port`. Tests cover display names, CamelCase, `ORE`/`ORST`, Beavers, and game keys. Ohio State / Oklahoma State / bare `OSU` / `PSU` remain allowed.

## Proof commands

```bash
# Import path (idempotent; needs sbro_ncaaf on disk)
dotnet run --project PossiblyMaybe.Cli -- import-disk-odds

# Tier A folds + train-only sweep + Tier C look-once
dotnet run --project PossiblyMaybe.Cli -- sweep-ncaaf
```

## Not done here

- Live slate / shadow ledger wire-up (A1 candidate store).
- CFBD 2023–25 O→C (needs free key).
- Pace×efficiency college model (scoring EMA only).
- Merge to main / push.
