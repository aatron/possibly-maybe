# Training-process review (Round 4 D3)

**Close:** 2026-11-07 · **Sources:** `book_round2/` (PLAN protocol, `audit_overcorrection.md`, `eval_*.md`), `round3/` (value-proof rules, W2/W7 proofs), `round4/` Wave R + A3/A5/R10 proofs.  
**Scope:** compare downloaded-book validation guidance to our process; list remaining gaps. Not a re-read of `library/`.

## Book guidance → our process

| Source (eval / paper) | Guidance | Ours |
|---|---|---|
| ISL / ESL model assessment; fpp3 §5.10 TSCV; CASI CV | Rolling-origin train&lt;k / test=k; do not reuse a single holdout for selection | **Tier A** walk-forward, folds pre-declared; **Tier B** burned report-only; **Tier C** look-once vault |
| PBO / DSR articles (`audit_overcorrection`) | Count trials; deflate reported performance | **N_trials** on reports; R10 trial ledger + nested grid charges `grid.Count`; BH q=0.10 on radar / seasonality |
| BDA3 / ROS model check; Rethinking overfit | Prefer simple predictive checks; ≤ few free params | Protocol ≤3 new params; money path = CLV + PnL (accuracy-only → IGNORE) |
| Gneiting–Raftery / fpp3 scoring | Proper scores for P(over); don’t optimize improper proxies alone | PLAN §6: log-loss/Brier **and** Kelly on same tickets for distributional changes |
| Angelopoulos conformal | Abstain / selective prediction | B8 `+conformal` registered; Tier A fold table still deferred |
| Kaunitz / market structure | Cross-venue soft spots | B6 live-only; warehouse TOTAL historically Consensus-only |

Protocol locked in book round 2 (`PLAN.md` evaluation protocol + A1 tightenings) and carried into rounds 3–4. Wave R encodes the hard parts in code rather than discipline alone.

## What Wave R closed (vs 2026-10-28 audit)

| Audit / book gap | Fix | Proof |
|---|---|---|
| No Domain `asOf`; live rebuild from “all scored games” | `PointInTime` + slate builders train tipoff &lt; asOf | `r1_proof.md` |
| Decisions not re-runnable | Append-only decision log (run id, SHA, watermark, venue lines, injury) | `r2_proof.md` |
| Cross-book closes as “CLV” | Same-book close only; cross-book flagged | `r3_proof.md` |
| Live CloseLine = finals (r2 critical) | FinalTotal separate (r3 W5); R3 same-book rule | r3 W5 + `r3_proof.md` |
| DK −110 assumed as real | PaperChamp real DK; assumed excluded from CLV | `r6_proof.md` (before: **0%** real on 180 DK tickets) |
| Strategy ≠ live path | `AsOfView` + `IStrategy` + day engine | `r7` / `r8` / `r9_proof.md` |
| Holdout peek / sim-only promotion | Vault + nested walk-forward + PromotionPolicy → Shadow only | `r10_proof.md` |
| Many cells / winner’s curse | Wave radar CUSUM + BH q=0.10 + 3-week persistence | `a3_proof.md` |
| Season-phase soft spots | Pre-registered phase windows before scan | `a5_proof.md` (table pending first run) |

## Gaps still open

1. **A5 wave calendar unscored** — phases locked; BH table empty until `seasonality-scan` on a backfilled `variants.db` (`a5_proof.md`).
2. **Radar ignores seasonality prior** — A3 CLV-only; A5 not wired (`a3_proof.md` limits).
3. **Deferred Tier A fold tables** — B4 soccer dists, B5 refs, B7 fitted/strict/EV-band, B8 conformal/kalman registered but fold CLV vs V0 not published (`b4`/`b5`/`b7`/`b8_proof.md`).
4. **Same-book ≤15m freshness ~0%** across leagues in R3 measure — denser pre-tip tracker needed (`r3_proof.md`).
5. **Vault durability** — in-process only; CLI `--sim` store does not survive restarts (`r10_proof.md` shortcut).
6. **Serie A keep/fix/drop** — still D2; provisional tiny-sample caution from audit still applies until fold verdict.
7. **Historical DC asOf shortcut** — live slate rebuilds asOf−3y; walk-forward backfill does not rebuild rolling DC per game (`r1_proof.md`).
8. **Assumed −110 warehouse / AssumedJuice** — still labeled; many historical O→C rows are Consensus → excluded from same-book CLV until tracker fills venue closes.

## Verdict

Process now matches the books on the money-critical axes: rolling-origin selection, look-once vault, trial counting, leakage barriers, and no sim→paper promotion. Remaining gaps are **execution** (run deferred Tier A / A5 scan, denser closes, durable vault) and **verdicts** (D2 Serie A), not missing protocol ideas.
