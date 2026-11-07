# Round 5: progress

## ▶ RESUME HERE
**Next action:** wait for wave-2 Composer agents (T4a/b/c, T5, T6); `--no-ff` merge, then T7.
Never redo a `done` row. Specs = `PLAN.md` § per item.

## Ledger
| Id | Item | Agent | Worktree / branch | Proof | Status | Merge |
|---|---|---|---|---|---|---|
| T1 | Durable vault + trial ledger | Composer | `../pm-r5-t1` `r5/t1` | `t1_proof.md` restart double-burn refused | done | 3ba67bc |
| T2a | Import NHL SBRO | Composer | `../pm-r5-t2a` `r5/t2a` | `t2a_proof.md` — 15 szn, 18943 O+C, Anaheim≠Oregon | done | 809b525 |
| T2b | Import AUS NFL totals | Composer | `../pm-r5-t2b` `r5/t2b` | `t2b_proof.md` — 2022 filled; 88.4% real prices | done | abfa5f4 |
| T3 | Two-week ESPN line backfill | Composer | `../pm-r5-t3` `r5/t3` | `t3_proof.md` — AsOfView can't see close | done | 2f63518 |
| T4a | Tier A once: soccer dists + pooling | Composer | `../pm-r5-t4a` `r5/t4a` | fold table vs V0 | running | |
| T4b | Tier A once: refs-nba, fitted / strict / EV-band | Composer | `../pm-r5-t4b` `r5/t4b` | fold table vs V0 | running | |
| T4c | Tier A once: conformal, kalman, soccer-strict | Composer | `../pm-r5-t4c` `r5/t4c` | fold table vs V0 | running | |
| T5 | Seasonality scan + radar prior | Composer | `../pm-r5-t5` `r5/t5` | `t5_proof.md` — N_trials=0; CUSUM prior wired | done (branch) | |
| T6 | NHL + NFL re-test | Composer | `../pm-r5-t6` `r5/t6` | Tier A / C once | running | |
| T7 | `engine-walk` + two-week replay before vs after | Composer | `../pm-r5-t7` `r5/t7` | no-peek walk test; report | planned | |
| T8 | Green tests, clean run, push | parent | main | tests + push | planned | |

## Log
| When | Event |
|---|---|
| 2026-11-07 | Round 5 plan written (plan only, per user). |
| 2026-11-07 | User: implement. Wave 1 launched (Composer): worktrees `pm-r5-t1/t2a/t2b/t3` on `r5/*`. |
| 2026-11-07 | T1 merged `--no-ff`: durable vault + trial ledger (`data/vault.db`); 282 tests. |
| 2026-11-07 | T2a merged `--no-ff`: `import-nhl-sbro`, 15 seasons × 18943 O+C (100%); Anaheim≠Oregon. |
| 2026-11-07 | T2b merged `--no-ff`: `import-nfl-aus`, 2022 filled, 88.4% real prices. |
| 2026-11-07 | T3 merged `--no-ff`: ESPN lines 2026-10-24→11-06 (406/409 O+C); AsOfView no-peek. |
| 2026-11-07 | Wave 2 launched (Composer): T4a/b/c, T5, T6. |
| 2026-11-07 | T5 on `r5/t5`: seasonality-scan N_trials=0 (no settled CLV); radar CUSUM prior h=7 + synthetic tests; `t5_proof.md`. |
