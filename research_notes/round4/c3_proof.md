# C3 proof: NFL redo — pack + Tier A O→C + nfl-wind

**Branch:** `r4/c3`  
**Date:** 2026-11-07  
**DB:** `/home/a/source/projects/possibly-maybe/possiblymaybe.db` (worktree has no local warehouse)

## What landed

| Piece | Path |
|---|---|
| Pack | `PossiblyMaybe.Domain/SportPacks/NflSportPack.cs` |
| Research gates (HOLD FAIL) | `PossiblyMaybe.Domain/NflChampConfig.cs` |
| Wind arm | `PossiblyMaybe.Domain/NflWind.cs` (`nfl-wind`) |
| Shadow series notes | `PossiblyMaybe.Domain/SportPacks/NflShadowMarkets.cs` |
| Backtest + fold path | `PossiblyMaybe.Cli/Backtest/NflTotalsBacktest.cs` |
| Tier A CLI | `PossiblyMaybe.Cli/Backtest/NflWindTierA.cs` → `tier-a-nfl-wind` |
| Tests | `SportPackTests.Nfl_Pack_Matches_ChampConfig_Empty_Oregon` |

Oregon: pack `OregonNames: []` (no NFL franchise in Oregon); cardinal rule still via global name list.

## Tier A (`nfl-wind` alone)

Command: `dotnet run --project PossiblyMaybe.Cli -- tier-a-nfl-wind <db>`

Compare EPA+roof **without** wind vs same + `NflWind` (≥15 mph → −3 pts expected total). Filled OpenClose only. Holdout **2024** burned.

Weather: `NflGameContexts=6502` withWind=`4597` extreme(≥15)=`610`.

| fold | n0 | clv0 | pnl0 | nW | clvW | pnlW | dPnL |
|---|---:|---:|---:|---:|---:|---:|---:|
| 2015 | 16 | −10.09 | −2.6 | 21 | −7.43 | +1.9 | +4.5 |
| 2016 | 17 | +1.09 | −5.5 | 23 | +1.26 | −7.7 | −2.2 |
| 2017 | 36 | +0.54 | +2.2 | 38 | +0.58 | +2.1 | −0.1 |
| 2018 | 40 | +1.10 | +0.2 | 43 | +1.31 | +2.9 | +2.7 |
| 2019 | 38 | +1.12 | +6.9 | 40 | +1.10 | +6.8 | −0.1 |
| 2020 | 36 | −4.61 | −9.2 | 44 | −2.66 | −1.9 | +7.3 |
| 2021 | 63 | +1.79 | +2.0 | 70 | +1.67 | +8.4 | +6.4 |
| 2023 | 59 | −0.20 | −11.3 | 58 | −0.05 | −8.4 | +2.9 |

**SUMMARY** folds=8 winFolds=**5/8** nW=337 pnl0=−17.4 pnlW=+4.1 meanClv0=−0.334 meanClvW=−0.022  

**VERDICT: Tier A PASS** (majority ΔPnL, CLV≥base, n≥40).

This is **not** a money promotion: prior NFL HOLD FAIL (hunt_nfl.md) still stands for the full EPA+roof+wind blend on 2024 holdout. `nfl-wind` only wins vs the no-wind baseline on walk-forward O→C folds.

## Live shadow / 1H / team totals

- Full-game: tracker already polls `KXNFLTOTAL` (`line_tracker.py`).
- Scaffold: `NflShadowMarkets.ShadowTrackerSeries` = `KXNFLTOTAL`, `KXNFL1HTOTAL`, `KXNFLTEAMTOTAL`.
- **Not wired into** `line_tracker.py` SPORTS yet (shortcut comment on pack helper).
- **No** `DailySlate` NFL emission → shadow ledger variant id not registered (would need A1 + slate NFL candidates).

## Runnable vs not (this worktree)

| Runnable | Not runnable here |
|---|---|
| Unit tests (pack / wind math) | Local `possiblymaybe.db` / `data/raw/nfl/*` (gitignored; use main paths) |
| Tier A against main DB | Live NFL slate / paper-go |
| Pack registry `Get("NFL")` | 1H/team O→C Tier A (no free history) |

## shortcut notes in code

- EPA CSV: worktrees omit bulky raw → `DefaultEpaCsvPath` falls back to `../possibly-maybe/data/raw/nfl/pregame_epa.csv`.
- Tracker: only full-game Kalshi series polled until A2 generalises.
- Season phases: placeholders until A5.
