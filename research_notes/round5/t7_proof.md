# T7 proof: multi-day engine walk + before/after report

**Date:** 2026-11-07  
**Branch:** `r5/t7` · **Worktree:** `../pm-r5-t7`  
**Owns:** `PossiblyMaybe.Cli/Replay/EngineWalk.cs`, `VariantStrategy.cs`, `engine-walk` CLI, DayEngine report fill, `EngineWalkTests`, `reports/replay_r5_2026-10-24_to_11-06.md`.

## Spec coverage

| Requirement | How |
|---|---|
| `engine-walk <from> <to> [strategies…]` | CLI + `EngineWalk.RunAsync` |
| Per day: AsOfView → Decide → 6 lanes × venues → reveal/settle | DayEngine.Run + in-memory bankrolls; **no paper.db** |
| CLV vs T3 close, same-book only else flagged | `SameBookClose.BookForVenue` + DraftKings close snaps |
| Flat + lane PnL | Flat @ DK units; all venue×scheme $ |
| Before = strategy set @ `84b3556` | Pinned `EngineWalk.Round4CloseSha`; arm `V0-paper` |
| After = paper champs + shadow PASS listed separately | Paper = `V0-paper`; shadows = mls-dc / +poisson-all / +negbin / +dc-rho / refs-nba / V-FC-NBA / kalman |
| Oregon exclusion + count | Inside Evaluate; report `OregonExcludedGames` |
| Report path + reading guidance | `reports/replay_r5_2026-10-24_to_11-06.md` |
| 3-day synthetic no-peek test | `EngineWalkTests.ThreeDay_Walk_Day2_Decide_Cannot_See_Day2_Finals_Or_Closes` |

## Headline (window 2026-10-24 → 2026-11-06)

From the committed report run (Oregon excluded=12, close-only skips=15):

| Config | Strategy | Bets | W-L-P | Flat u | Mean CLV | Checkpoint |
|---|---|---:|---|---:|---:|---|
| Before (`84b3556`) | V0-paper | 102 | 48-41-0 | +0.83 | +0.180 | BailCheck/hold |
| After paper | V0-paper | 102 | 48-41-0 | +0.83 | +0.180 | same arm |
| After shadow | +negbin | 93 | 44-38-0 | +2.00 | +0.220 | BailCheck/hold |
| After shadow | +poisson-all / +dc-rho | 119 | 55-51-0 | −1.00 | +0.198 | ClvVerdict/keep |
| After shadow | kalman | 122 | 57-52-0 | −0.18 | +0.193 | ClvVerdict/keep |
| After shadow | refs-nba / V-FC-NBA | 0 | — | — | — | (no RefTotalsResidual / NBA-only) |

Lane $ (all venues×schemes) is large negative noise at this n — not a stake verdict.

## Tests

```bash
dotnet build PossiblyMaybe.Tests -c Release
dotnet PossiblyMaybe.Tests/bin/Release/net10.0/PossiblyMaybe.Tests.dll --disable-logo
# 321 succeeded (incl. 3 EngineWalkTests)
dotnet PossiblyMaybe.Tests/bin/Release/net10.0/PossiblyMaybe.Tests.dll \
  --treenode-filter '/*/PossiblyMaybe.Tests/EngineWalkTests/*' --disable-logo
```

| Claim | Result |
|---|---|
| Day-2 AsOfView hides day-2 final + close; Decide uses open line | PASS |
| Settle CLV = close − open (OVER); Oregon never ticketed | PASS |
| `V0-paper` / shadow ids resolve | PASS |

## Reproduce walk

```bash
# T3 opens must be on the warehouse (copied from pm-r5-t3 or re-run backfill-espn-lines).
dotnet run -c Release --project PossiblyMaybe.Cli -- engine-walk 20261024 20261106 \
  /path/to/possiblymaybe.db \
  --report reports/replay_r5_2026-10-24_to_11-06.md
```

## Limits

- shortcut: live ESPN scoreboard still builds the day slate; warehouse open lines overlay for betting (T3). Offline/historical ESPN misses → warehouse-only stub candidates (model=line → usually no edge).
- shortcut: `refs-nba` needs `RefTotalsResidual` on candidates (DailySlate does not fill) → 0 bets in this window.
- shortcut: non-DK venues fee-adjust the same open american (no historical Kalshi/Crypto asks).
- Lane $ sums four venues × six schemes from $1000 seeds — large drawdowns are expected noise at n≈100.
- Replay is **not** a clean holdout (PLAN reading guidance); no promotion from these numbers.

## Not done (by design)

- No `paper-go` / `settle-go`. OregonExclusion untouched. No push/merge to main.
