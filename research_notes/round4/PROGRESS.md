# Round 4: progress

## ▶ RESUME HERE
**Next action:** wait for C1–C5 + post-A1 (A5, B1–B9, C7) + C6-epl/bundesliga/nhl; `--no-ff` merge in ownership order. Wave R (R1–R6) still open. D* at end.
Never redo a `done` row. Specs = `PLAN.md` § Wave tables. r3 W16 still external — reassign off Opus when that session can stop.

## Ledger
| Id | Item | Agent | Worktree / branch | Proof | Status | Merge |
|---|---|---|---|---|---|---|
| A1 | Candidate store + variants-as-filters + backfill | Composer | `../pm-r4-a1` `r4/a1` | V0/V3 reproduce; 1-file variants | done | a537a6c |
| A2 | Line tracker all leagues + importer | Composer | `../pm-r4-a2` `r4/a2` | O+C coverage ≥ 80% | done | 597cd2a |
| A3 | Wave radar + BH + trial registry + ladder | Composer | `../pm-r4-a3` `r4/a3` | synthetic detection | done | 3ddce67 |
| A4 | League / venue census | Auto / Composer | notes | ranked list | done | league_census.md |
| A5 | Seasonality scan | Composer | `../pm-r4-a5` `r4/a5` | phase table (BH) | running | |
| B1 | Availability: +absence, +injury-guard | Composer | `../pm-r4-b1` `r4/b1` | Tier A / live | running | |
| B2 | +pooling, +roster-reset | Composer | `../pm-r4-b2` `r4/b2` | Tier A / live | running | |
| B3 | nba-volume, nba-volume-efg | Composer | `../pm-r4-b3` `r4/b3` | Tier A | running | |
| B4 | mls-dc, +poisson-all, +negbin, +dc-rho | Composer | `../pm-r4-b4` `r4/b4` | Tier A | running | |
| B5 | refs-nba totals | Composer | `../pm-r4-b5` `r4/b5` | Tier A | running | |
| B6 | consensus-outlier, line-move-follow | Composer | `../pm-r4-b6` `r4/b6` | live | running | |
| B7 | +fitted-curve, strict / EV-band | Composer | `../pm-r4-b7` `r4/b7` | Tier A | running | |
| B8 | +conformal, kalman | Composer | `../pm-r4-b8` `r4/b8` | Tier A | running | |
| B9 | inv-var-blend | Composer | `../pm-r4-b9` `r4/b9` | Tier A | running | |
| C0 | Sport-pack template | Composer | `../pm-r4-c0` `r4/c0` | MLS identical | done | eb3ad96 |
| C1 | NCAAB totals | Composer | `../pm-r4-c1` `r4/c1` | Tier A / C | running | |
| C2 | NCAAF totals | Composer | `../pm-r4-c2` `r4/c2` | Tier A / C | running | |
| C3 | NFL redo + wind + sub-markets | Composer | `../pm-r4-c3` `r4/c3` | Tier A | running | |
| C4 | MLB redo + F5 ready | Composer | `../pm-r4-c4` `r4/c4` | Tier A / C | running | |
| C5 | La Liga + EFL shadow | Composer | `../pm-r4-c5` `r4/c5` | Tier A | running | |
| C6-epl | EPL pack (A4 #1 ready) | Composer | `../pm-r4-c6-epl` `r4/c6-epl` | Tier A | running | |
| C6-bundesliga | Bundesliga pack (A4 #2 ready) | Composer | `../pm-r4-c6-bundesliga` `r4/c6-bundesliga` | Tier A | running | |
| C6-nhl | NHL pack (A4 #3; Tier A O→C-gated) | Composer | `../pm-r4-c6-nhl` `r4/c6-nhl` | shadow + tracker | running | |
| C7 | Spreads + moneylines | Composer | `../pm-r4-c7` `r4/c7` | Tier A | running | |
| R1 | As-of cutoff in all model builders | Composer | A1 amendment or `../pm-r4-r1` | leak test | pending (amend A1) | |
| R2 | Append-only decision log (run id, SHA, watermark, prices, injury snapshot) | Composer | A1 amendment | schema test | pending (amend A1) | |
| R3 | Same-book close | Composer | `../pm-r4-r3` `r4/r3` | % same-book close | running | |
| R4 | replay --asof command | Composer | `../pm-r4-r4` | 11-07 diff = 0 | pending (A1, R1, R2) | |
| R5 | Checkpoint reporter (50/100–200/500/1500) | Composer | `../pm-r4-r5` `r4/r5` | stage test | running | |
| R6 | Real DK price on paper tickets | Composer | `../pm-r4-r6` | % real price | pending | |
| D1 | True close on every league's settle | Composer | — | check | pending | |
| D2 | Serie A verdict | Auto / Composer | `../pm-r4-d2` | fold table | pending (A1, B4) | |
| D3 | Training-process review | Auto / Composer | reports | — | pending (end) | |
| D4 | Round-4 waves report | Auto / Composer | reports | — | pending (end) | |
| D5 | Green tests, clean run, push | parent | main | — | pending | |
| r3-W16 | archive.org odds (carried) | Auto / Composer (was Opus/Claude Code) | notes + data/raw | O→C seasons | running — reassign off Opus if still mid-flight | |

## Log
| When | Event |
|---|---|
| 2026-11-07 | Round 3 finished (main @ ecde719). Round 4 plan + checklist written. |
| 2026-11-07 | Wave A launched: worktrees `pm-r4-a1/a2/a3/c0` on `r4/*`; A4 census research started. |
| 2026-11-07 | Plan: no Opus — Auto/Composer only. A4 Opus could not be interrupted mid-run; fresh Auto/Composer A4 launched (overwrite any Opus census). r3-W16 still on external Claude Code — reassign off Opus when that session can stop. |
| 2026-11-07 | C0 merged `--no-ff` → main @ eb3ad96 (162 tests). Launched C1–C5 worktrees. |
| 2026-11-07 | A2 merged @ 597cd2a; A1 merged @ a537a6c (167 tests, 11 py). Launched A5 + B1–B9 + C7. |
| 2026-11-07 | User: record data so any model can be re-run without cheating → Wave R (R1–R6) added; R1/R2 should amend A1 while it is running. |
| 2026-11-07 | A3 merged @ 3ddce67 (172 tests). A4 census done. Launched C6-epl, C6-bundesliga, C6-nhl. |
