# Round 5: progress

## ▶ RESUME HERE
**Next action:** Round 5 closed (T8). Push done.
Never redo a `done` row. Specs = `PLAN.md` § per item.

## Ledger
| Id | Item | Agent | Worktree / branch | Proof | Status | Merge |
|---|---|---|---|---|---|---|
| T1 | Durable vault + trial ledger | Composer | `../pm-r5-t1` `r5/t1` | `t1_proof.md` restart double-burn refused | done | 3ba67bc |
| T2a | Import NHL SBRO | Composer | `../pm-r5-t2a` `r5/t2a` | `t2a_proof.md` — 15 szn, 18943 O+C, Anaheim≠Oregon | done | 809b525 |
| T2b | Import AUS NFL totals | Composer | `../pm-r5-t2b` `r5/t2b` | `t2b_proof.md` — 2022 filled; 88.4% real prices | done | abfa5f4 |
| T3 | Two-week ESPN line backfill | Composer | `../pm-r5-t3` `r5/t3` | `t3_proof.md` — AsOfView can't see close | done | 2f63518 |
| T4a | Tier A once: soccer dists + pooling | Composer | `../pm-r5-t4a` `r5/t4a` | `t4a_proof.md` — dists PASS; pooling FAIL | done | 7b68dc8 |
| T4b | Tier A once: refs-nba, fitted / strict / EV-band | Composer | `../pm-r5-t4b` `r5/t4b` | `t4b_proof.md` — refs+FC PASS Shadow; strict/EV FAIL | done | acf5e64 |
| T4c | Tier A once: conformal, kalman, soccer-strict | Composer | `../pm-r5-t4c` `r5/t4c` | `t4c_proof.md` — kalman PASS; conformal+strict FAIL | done | fe3bfa8 |
| T5 | Seasonality scan + radar prior | Composer | `../pm-r5-t5` `r5/t5` | `t5_proof.md` — N_trials=0; CUSUM prior wired | done | 55a2a18 |
| T6 | NHL + NFL re-test | Composer | `../pm-r5-t6` `r5/t6` | `t6_proof.md` — NHL FAIL/TierC PASS; NFL FAIL; wind PASS | done | c5da041 |
| T7 | `engine-walk` + two-week replay before vs after | Composer | `../pm-r5-t7` `r5/t7` | `t7_proof.md` + replay report | done | b5d9922 |
| T8 | Green tests, clean run, push | parent | main | tests + push | done | (this) |

## Log
| When | Event |
|---|---|
| 2026-11-07 | Round 5 plan written (plan only, per user). |
| 2026-11-07 | User: implement. Wave 1 launched (Composer): worktrees `pm-r5-t1/t2a/t2b/t3` on `r5/*`. |
| 2026-11-07 | T1–T3 merged (vault, NHL SBRO, AUS NFL, ESPN two-week lines). |
| 2026-11-07 | Wave 2 launched (Composer): T4a/b/c, T5, T6. |
| 2026-11-07 | T5 merged: seasonality BH prior (CUSUM h 10→7); N_trials=0 settled CLV. |
| 2026-11-07 | T4b merged: refs-nba + V-FC-NBA PASS→Shadow; strict/EV FAIL. |
| 2026-11-07 | T4a merged: soccer dists mostly PASS→Shadow; +pooling FAIL; N_trials=17. |
| 2026-11-07 | T4c merged: kalman PASS→Shadow; +conformal + soccer-strict FAIL; N_trials=3. |
| 2026-11-07 | T6 merged: NHL Tier A FAIL / Tier C look-once PASS (vault burned); NFL FAIL; nfl-wind PASS. |
| 2026-11-07 | T7 merged: engine-walk + replay (before=after paper +0.83u CLV +0.180). |
| 2026-11-07 | T8: tests green; pm-calculate clean; push origin main. Round 5 closed. |
