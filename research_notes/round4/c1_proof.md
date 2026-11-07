# C1 proof: NCAAB totals pack

**Branch:** `r4/c1`  
**Date:** 2026-11-07  
**DB:** main `possiblymaybe.db` (W15 SBRO NCAAB already imported)

## Tier C holdout — locked **before** tuning

| Lock | Value |
|---|---|
| Train max | `2020-21` |
| **Tier C holdout** | **`2021-22`** (last SBRO season on disk) |
| Skip | `≥2022-23` (no O→C yet; live season = shadow only) |
| Tier A fold labels | `2015-16` … `2020-21` (pre-registered; sparse 2012–15 avoided) |

Holdout string lives on `NcaabSportPack.Instance.TierCHoldoutSeason` and `NcaabTotalsBacktest.HoldoutSeason`. Champ gates in `NcaabChampConfig` were set provisionally (`w=0.50`, `|edge|≥5`, EV≥4%) **before** the sweep; sweep selection is train-PnL only, then holdout is scored once for the winner. Provisional config was **not** rewritten to the train-max cell.

## What landed

| Piece | Path |
|---|---|
| Champ consts | `PossiblyMaybe.Domain/NcaabChampConfig.cs` |
| Pack | `PossiblyMaybe.Domain/SportPacks/NcaabSportPack.cs` |
| Registry | `SportPackRegistry.Ncaab` |
| Oregon / NCAA aliases | `SportTeams.NcaaCode` / `TryNcaa` / `ResolveNcaa` + `OregonExclusion` |
| Backtest / sweep | `PossiblyMaybe.Cli/Backtest/NcaabTotalsBacktest.cs` · CLI `sweep-ncaab` |
| Live shadow slate | `DailySlate.BuildNcaabCandidatesAsync` / `RunNcaabAsync` · `slate-ncaab` · hooked in `variants-shadow` |
| Tests | `SportPackTests.Ncaab_*`, `SbroXlsxParserTests.NcaaCode_Unmapped*`, existing Oregon suite |

## Model

- Prefer `PaceEfficiencyBook` when a `GameBox` exists; else `ScoringBook` (α=0.15, leagueInit=140).
- SBRO path has scores + O→C only → almost all history uses ScoringBook today (same fallback pattern as NBA when boxes missing).
- Assumed juice −110; Oregon cardinal sides never bet (still update ratings).
- Importer path unchanged: `import-disk-odds` → `DiskOddsImporter` SBRO NCAAB folder.

## Tier A / C result (look-once holdout)

Sweep grid (train selection band: CLV>0, PnL>0, bets 80…8000):

| | Train | Holdout `2021-22` |
|---|---|---|
| **Selected (train max PnL)** | w=0.50 edge=4.0 · n=7346 · CLV=+13.99 · PnL=+1480.7u | n=468 · CLV=+2.74 · hit=57.1% · PnL=+44.7u |
| Provisional champ (unchanged) | w=0.50 edge=5.0 | n=182 · CLV=+3.91 · PnL=+36.6u |

**KILL: HOLD PASS** on the train-selected cell (CLV+PnL, n≥40). Remain **shadow-only** for paper until wave radar / promotion ladder (A3). Do not retune `NcaabChampConfig` on `2021-22`.

## Oregon cardinal

Pack `OregonNames` cover Oregon Ducks / Oregon State / Portland State / Portland Pilots (college) name + code variants; `OregonNamesExcluded()` asserts each is excluded for `NCAAB`. Unmapped schools get `SanitizeNcaa` tokens and never hit the Oregon-only alias map (`TryNcaa("Duke")` is null).

## Live shadow

- `variants-shadow` appends NCAAB ESPN mens-college-basketball candidates when the scoreboard responds.
- `slate-ncaab` prints GO/EXCLUDED/NO-GO and records shadow rows.
- Warehouse NCAAB results still end at `2021-22` → slate will usually print **STALE DATA** / no paper GOs until ESPN refresh lands more finals; shadow candidate emission still runs.

## Not done (leftover)

- True pace×eff needs NCAAB `GameBoxes` (ESPN box import).
- Post-2022 O→C (CBBD / Odds API) for a fresher Tier C later — do **not** burn `2021-22` again.
- ~360-team alias polish beyond Oregon cardinal + sanitize (unmapped keep own name).
- No merge / no push from this branch.
