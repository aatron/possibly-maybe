# C6-nhl proof: NHL sport pack + shadow / tracker (Tier A O→C-gated)

**Branch:** `r4/c6-nhl`  
**Date:** 2026-11-07  
**Census:** A4 #3 — ready for C6-nhl; live venues strong; historical free O→C thin.

## Verdict

| Path | Status |
|---|---|
| Sport pack (C0 template) | **shipped** — `NhlSportPack` / `NhlShadowConfig` |
| Shadow + line_tracker | **shipped** — ESPN / Kalshi `KXNHLTOTAL` / Crypto.com `NHL` |
| Tier A walk-forward | **shadow-only** — no free multi-season O→C in warehouse; `TierAFolds=[]`, `TierAReady=false` |
| Oregon | Winterhawks names kept excluded (no NHL franchise) |

Promote Tier A only after tracker age or Odds API fills same-book open→close.

## What landed

| Piece | Path |
|---|---|
| Shadow gates | `PossiblyMaybe.Domain/NhlShadowConfig.cs` |
| Pack | `PossiblyMaybe.Domain/SportPacks/NhlSportPack.cs` |
| Registry | `SportPackRegistry` + `Nhl` |
| Aliases | `SportTeams.TryNhl` / `ResolveNhl` (32 clubs) |
| Tracker | `scripts/line_tracker.py` SPORTS `NHL` |
| Quotes | `KalshiQuotes` `KXNHLTOTAL`; `CryptoComQuotes` kind `NHL` |
| Results | `ResultsRefresh` ESPN `hockey/nhl` (ET KeyDate) |
| Slate / shadow | `DailySlate.RunNhlAsync` / `BuildNhlCandidatesAsync`; `VariantShadow` + `slate-nhl`; `pm-calculate.sh` |
| Importer | `LineTrackerImporter.ResolveTeam("NHL", …)` |
| Strike | `StrikeMatch` NHL Δ ≤ 0.5 |
| Tests | `SportPackTests.Nhl_*`, Oregon / Kalshi / ResultsRefresh / LineTracker / `test_line_tracker.py` |

## Identical C0 shape

Pack fields match MLS template: results source, odds sources, rating book, Oregon names + `ResolveAlias`, season phases, Tier A folds (empty = gated), train/holdout/skip placeholders (`none`), V0-style gates from `NhlShadowConfig` consts (no duplicated magic).

## Shadow path (how verified)

1. `OregonNamesExcluded()` true for Winterhawks / winterhawks under league id `NHL`.
2. `NhlSportPack.TierAReady == false` while `TierAFolds` empty.
3. Live slate prints `SHADOW-ONLY NHL` and records candidates; no `paper-go` emission.
4. `variants-shadow` pulls NHL board beside NBA/soccer.
5. Tracker config asserts espn/kalshi/cdc wired.

## Not done (intentional)

- Tier A folds / holdout seasons — wait for O→C fill.
- Historical backfill into `VariantBackfill` — no odds archive to walk.
- Full live `slate-nhl` against tonight’s scoreboard (unit proof only).
