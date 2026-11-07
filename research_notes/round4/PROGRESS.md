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
| B2 | +pooling, +roster-reset | Composer | `../pm-r4-b2` `r4/b2` | Tier A / live | done | ef60fef |
| B3 | nba-volume, nba-volume-efg | Composer | `../pm-r4-b3` `r4/b3` | Tier A | running | |
| B4 | mls-dc, +poisson-all, +negbin, +dc-rho | Composer | `../pm-r4-b4` `r4/b4` | Tier A | running | |
| B5 | refs-nba totals | Composer | `../pm-r4-b5` `r4/b5` | Tier A | done | e9a831f |
| B6 | consensus-outlier, line-move-follow | Composer | `../pm-r4-b6` `r4/b6` | live | running | |
| B7 | +fitted-curve, strict / EV-band | Composer | `../pm-r4-b7` `r4/b7` | Tier A | running | |
| B8 | +conformal, kalman | Composer | `../pm-r4-b8` `r4/b8` | Tier A | running | |
| B9 | inv-var-blend | Composer | `../pm-r4-b9` `r4/b9` | Tier A FAIL → shadow only | done | 10ef936 |
| C0 | Sport-pack template | Composer | `../pm-r4-c0` `r4/c0` | MLS identical | done | eb3ad96 |
| C1 | NCAAB totals | Composer | `../pm-r4-c1` `r4/c1` | Tier A / C | running | |
| C2 | NCAAF totals | Composer | `../pm-r4-c2` `r4/c2` | Tier A / C | done | cce3dd5 |
| C3 | NFL redo + wind + sub-markets | Composer | `../pm-r4-c3` `r4/c3` | Tier A | running | |
| C4 | MLB redo + F5 ready | Composer | `../pm-r4-c4` `r4/c4` | Tier A / C | done | d01ba86 |
| C5 | La Liga + EFL shadow | Composer | `../pm-r4-c5` `r4/c5` | Tier A | running | |
| C6-epl | EPL pack (A4 #1 ready) | Composer | `../pm-r4-c6-epl` `r4/c6-epl` | Tier A | running | |
| C6-bundesliga | Bundesliga pack (A4 #2 ready) | Composer | `../pm-r4-c6-bundesliga` `r4/c6-bundesliga` | Tier A | running | |
| C6-nhl | NHL pack (A4 #3; Tier A O→C-gated) | Composer | `../pm-r4-c6-nhl` `r4/c6-nhl` | shadow + tracker | running | |
| C7 | Spreads + moneylines | Composer | `../pm-r4-c7` `r4/c7` | Tier A | running | |
| R1 | As-of cutoff in all model builders | Composer | A1 amendment or `../pm-r4-r1` | leak test | pending (amend A1) | |
| R2 | Append-only decision log (run id, SHA, watermark, prices, injury snapshot) | Composer | A1 amendment | schema test | pending (amend A1) | |
| R3 | Same-book close | Composer | `../pm-r4-r3` `r4/r3` | % same-book close | running | |
| R4 | replay --asof command | — | — | — | superseded by R7–R9 (replay = engine over logged dates) | |
| R7 | Point-in-time AsOfView (no DB handle for strategies) | Composer | `../pm-r4-r7` | leak tests | pending (A1+R1/R2) | |
| R8 | IStrategy plug-in + port champions | Composer | `../pm-r4-r8` | reproduce Tier A/B | pending (R7) | |
| R9 | Day-stepping engine shared with live | Composer | `../pm-r4-r9` | live == engine today | pending (R7) | |
| R10 | Vault + trial ledger + nested tuning + no sim-only promotion | Composer | `../pm-r4-r10` | vault refusal test | pending (R8, R9) | |
| R5 | Checkpoint reporter (50/100–200/500/1500) | Composer | `../pm-r4-r5` `r4/r5` | stage test | running | |
| R6 | Real DK price on paper tickets | Composer | `../pm-r4-r6` | % real price | pending | |
| D1 | True close on every league's settle | Composer | — | check | pending | |
| D2 | Serie A verdict | Auto / Composer | `../pm-r4-d2` | fold table | pending (A1, B4) | |
| D3 | Training-process review | Auto / Composer | reports | — | pending (end) | |
| D4 | Round-4 waves report | Auto / Composer | reports | — | pending (end) | |
| D5 | Green tests, clean run, push | parent | main | — | pending | |
| r3-W16 | archive.org odds (carried) | **Composer only** (no Opus / Auto / subagents) | notes + data/raw | O→C seasons | **paused — see `W16_archive_org_HANDOFF.md`** (CDX indexes, MLB 2010–21 xlsx minus 2016, NFL aussportsbetting, NCAAF 2017-18 fuller, density table done; SBRO gap files + verify + notes remain) | |

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
| 2026-11-07 | B9 merged @ 10ef936 (174 tests). |
| 2026-11-07 | User: blind day-by-day sim for any new approach → R7–R10 added; recommend R7–R9 before wave B (variants written once as strategies). |
| 2026-11-07 | Merged B2, B5, C2, C4 (191+ tests). |
| 2026-11-07 | r3-W16 Opus agent stopped on user request; progress inventoried → `W16_archive_org_HANDOFF.md`. |
| 2026-11-07 | r3-W16 handoff tightened to **Composer only** (no Opus / Auto / Task subagents). |
| 2026-11-07 | Batch land: C1, C3, C5, C6-epl, R3, R5 on main. C6-bundesliga/nhl + B1/B3/B4/B6/B7/B8 still in worktrees (VariantCore/SportTeams merge debt). |
