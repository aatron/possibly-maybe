# MLB sport pack — Gate 0 / feature plan

**Do not change NBA champ paths.** Shared Domain + ledger only.

## Are we using baseball-critical metrics today?

**No.** Files are local (`mlb_closing_odds_2025`); importer/metrics not built yet.  
A naive “team runs EMA” (NBA-style scoring book) is **wrong for MLB** — the **starting pitcher** dominates the total.

## What sites say matters for MLB totals (ranked)

| Priority | Metric | Why | Free sources |
|---|---|---|---|
| 1 | **Starting pitcher** quality | Biggest single lever on the total | Probables: MLB.com; FIP/xFIP/SIERA/K-BB%: FanGraphs |
| 2 | **Park factor** | Coors etc. swing runs hard | Baseball Savant park factors |
| 3 | **Weather** (temp, wind out/in) | Especially open parks | NOAA / game-day feeds |
| 4 | Recent team offense / runs allowed | Secondary to SP | Baseball-Reference / FanGraphs |
| 5 | Lineup / platoon (L/R) | Moves after lineup posts | MLB.com lineups |
| 6 | Bullpen rest | Smaller than SP/park on average | Box + recent IP |

ERA alone is weak (includes defense/luck). Prefer **FIP / xFIP / K-BB%**.

## Gate 0 (data)

- [x] M1 downloaded → `data/raw/kaggle/mlb_closing_odds_2025/mlb_2025_closing_odds.csv` (~2477 games, open/close totals)
- [ ] Import games + TOTAL open/close (`Sport=MLB`)
- [ ] Lock holdout **inside 2025** (only one open/close season) — e.g. train before 2025-09-01, holdout Sep–Nov — **freeze before peeking metrics**
- [ ] Gate 1 **only after** SP features exist (or explicitly kill “runs-only” as insufficient)

## Gate 1 feature order (kill if no OOS CLV)

1. Open/close totals + naive runs book (baseline — expect weak)
2. **Add probable SP FIP/xFIP** (must-have)
3. Park factor table
4. Weather when available
5. Run line only after totals process works

## Non-goals until SP works

Copying NBA Elo/pace recipes; umpires as first feature; props.
