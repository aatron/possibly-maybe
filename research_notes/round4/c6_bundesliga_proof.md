# C6-bundesliga proof

**Branch:** `r4/c6-bundesliga`  
**Date:** 2026-11-07  
**Census:** A4 #2 **ready for C6-bundesliga** (FD `D1`, Kalshi `KXBUNDESLIGATOTAL`, Crypto.com kind TBD / none aliased).

## Tier C lock (chosen before any tuning)

| Lock | Value | Notes |
|---|---|---|
| **TierCHoldoutSeason** | **`2025-2026`** | Fresh look-once. Do not tune α / w / edge / EV on this season. |
| TrainMaxSeason | `2024-2025` | Last season allowed in train / fold search. |
| SkipSeason | _(none)_ | No post-holdout incomplete season beyond Tier C yet. |
| TierAFolds | `2018-2019` … `2023-2024` | Walk-forward holdouts; gates frozen on `BundesligaShadowConfig`. |
| Gates (frozen defaults) | w=0.40, \|exp−line\|≥0.25, EV≥4%, α=0.18 | Same FAIL-league goals-EMA defaults as La Liga / EFL shadow. **Not tuned.** |

Kill gate when Tier A / C are later scored: holdout PnL > 0 **and** CLV > 0 **and** n ≥ 40. Until then the pack is **shadow-only** (no paper-go).

## What landed

| Piece | Path |
|---|---|
| Shadow gates | `PossiblyMaybe.Domain/BundesligaShadowConfig.cs` |
| Pack | `PossiblyMaybe.Domain/SportPacks/BundesligaSportPack.cs` |
| Registry | `SportPackRegistry` → `BUNDESLIGA` |
| Aliases | `SportTeams.TryBundesliga` (ESPN / FD → warehouse; **no Oregon clubs**) |
| Live shadow | `DailySlate` + `ResultsRefresh` (`soccer/ger.1`) + `VariantBackfill` |
| Tracker | `line_tracker.py` → Kalshi `KXBUNDESLIGATOTAL`, `cdc=None` (kind missing) |
| FD / Kaggle import | `scripts/fetch_fd_d1.py` → `data/raw/soccer/bundesliga/totals_open_close.csv`; or `export_soccer_league_totals.py --league Bundesliga --sport BUNDESLIGA`; then `import-soccer BUNDESLIGA bundesliga` |
| Test | `SportPackTests.Bundesliga_Pack_Locks_TierC_And_Oregon` |

## Oregon

No Bundesliga Oregon clubs. Pack `OregonNames: []`; every pick still goes through `OregonExclusion`. Alias tests assert common Bundesliga names are **not** excluded, and Oregon name variants stay excluded on other sports.

## Not done here

- Full Tier A / Tier C numeric run against a filled warehouse (needs Kaggle or FD CSV import into DB).
- Crypto.com kind map (census: none found).
- Paper promotion (shadow until WAVE ON + Tier proof).
