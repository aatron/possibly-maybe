# Round 5: progress

## ▶ RESUME HERE
**Next action:** wave 1 parallel items T1 / T2a / T3 (T2b done on branch). Merge only when user says.
Never redo a `done` row. Specs = `PLAN.md` § per item.

## Ledger
| Id | Item | Agent | Worktree / branch | Proof | Status | Merge |
|---|---|---|---|---|---|---|
| T1 | Durable vault + trial ledger | Composer | `../pm-r5-t1` `r5/t1` | restart double-burn refused | planned | |
| T2a | Import NHL SBRO | Composer | `../pm-r5-t2a` `r5/t2a` | seasons × O+C coverage; Anaheim ≠ Oregon | planned | |
| T2b | Import AUS NFL totals | Composer | `../pm-r5-t2b` `r5/t2b` | 2022 filled 284/284; 88.4% real 2014–25 | **done** | |
| T3 | Two-week ESPN line backfill | Composer | `../pm-r5-t3` `r5/t3` | decision can't see close | planned | |
| T4a | Tier A once: soccer dists + pooling | Composer | `../pm-r5-t4a` `r5/t4a` | fold table vs V0 | planned | |
| T4b | Tier A once: refs-nba, fitted / strict / EV-band | Composer | `../pm-r5-t4b` `r5/t4b` | fold table vs V0 | planned | |
| T4c | Tier A once: conformal, kalman, soccer-strict | Composer | `../pm-r5-t4c` `r5/t4c` | fold table vs V0 | planned | |
| T5 | Seasonality scan + radar prior | Composer | `../pm-r5-t5` `r5/t5` | BH phase table; synthetic wave | planned | |
| T6 | NHL + NFL re-test | Composer | `../pm-r5-t6` `r5/t6` | Tier A / C once | planned | |
| T7 | `engine-walk` + two-week replay before vs after | Composer | `../pm-r5-t7` `r5/t7` | no-peek walk test; report | planned | |
| T8 | Green tests, clean run, push | parent | main | tests + push | planned | |

## Log
| When | Event |
|---|---|
| 2026-11-07 | Round 5 plan written (plan only, per user). |
| 2026-11-07 | T2b done on `r5/t2b`: `import-nfl-aus`; 2022 O→C 284/284 real; 88.4% of 2014–25 NFL games have AUS real prices; assumed −110 kept; oregon=0; proof `t2b_proof.md`. |
