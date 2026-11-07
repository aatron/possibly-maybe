# Round 4: progress

## ▶ RESUME HERE
**Next action:** wait for A3/A4 + C1–C5 + post-A1 wave (A5, B1–B9, C7); `--no-ff` merge in ownership order. C6 waits on A4. D* at end.
Never redo a `done` row. Specs = `PLAN.md` § Wave tables. r3 W16 still external — reassign off Opus when that session can stop.

## Ledger
| Id | Item | Agent | Worktree / branch | Proof | Status | Merge |
|---|---|---|---|---|---|---|
| A1 | Candidate store + variants-as-filters + backfill | Composer | `../pm-r4-a1` `r4/a1` | V0/V3 reproduce; 1-file variants | done | a537a6c |
| A2 | Line tracker all leagues + importer | Composer | `../pm-r4-a2` `r4/a2` | O+C coverage ≥ 80% | done | 597cd2a |
| A3 | Wave radar + BH + trial registry + ladder | Composer | `../pm-r4-a3` `r4/a3` | synthetic detection | running | |
| A4 | League / venue census | Auto / Composer | notes | ranked list | running (fresh Auto/Composer; discard Opus write if any) | |
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
| C6 | New leagues (per A4) | Composer | `../pm-r4-c6-<league>` | per league | pending (A4) | |
| C7 | Spreads + moneylines | Composer | `../pm-r4-c7` `r4/c7` | Tier A | running | |
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
