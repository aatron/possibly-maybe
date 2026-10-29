# Book round 2 — topics review

**Baseline:** `main` @ book-round2 start · **Close:** 2026-10-29  
**Prime directive:** +CLV / +PnL soft spots, not prettier metrics.

## What merged (money path)

| Change | Where | Evidence |
|---|---|---|
| Poisson CDF `OverProb` | Serie A + Ligue 1 only | Tier A PASS; Tier C 2025–26 KEEP (`tier_c_poisson-cdf-pover.md`) |
| Fee-adjusted binary EV/Kelly | Kalshi / RH / Crypto | Unit proof; live hygiene (`exp_venue-fee-ev.md`) |
| Strike max-Δ gate | Kalshi / Crypto (+ RH via Kalshi) | Live hygiene (`exp_strike-max-delta.md`) |
| Settle hint fix | `pm-calculate.sh` | No longer passes final score as `closeLine` |

## What was tested and rejected

| Idea | Verdict | Why |
|---|---|---|
| NBA Normal residual-σ `OverProb` | KILL | Same GO set; worse Kelly (`exp_nba-normal-sigma-pover.md`) |
| Inverse-variance blend `w` | KILL | Tier A FAIL NBA/MLS (`exp_inv-var-blend-w.md`) |
| Poisson on MLS / Liga MX | FAIL | MLS Tier A CLV; Liga MX Tier C CLV |
| NFL totals (post O→C fill) | FAIL | Hold 2024 PnL −1.6 (`hunt_nfl.md`) |
| MLB totals (post 2011–21 fill) | FAIL | Hold Sep-2025 PnL −0.8; thin n (`hunt_mlb.md`) |

## Topic evals (sources → USED / TESTED / CANDIDATE / IGNORE)

Full chapter tables: `research_notes/book_round2/eval_*.md`. Consensus CANDIDATE that paid: **distributional P(over) from existing λs** (soccer Poisson). NBA Normal-σ and inv-var `w` were CANDIDATEs that failed money gates.

## Protocol

Tightened by A1 (`audit_overcorrection.md` → `PLAN.md`): no Tier B in grids, N_trials logged, Tier C look-once ledger, tiny-sample provisional, juice labeled AssumedJuice −110.

## Data

D1 downloads in `data/raw/round2_odds/` (gitignored). Imported via `import-round2-odds`: NFL flancast overlays + MLB 2011–2021 append. NBA free O→C for 2023–24 / 2025–26 still missing.

## Still open

- Phase 6 training-process review (`reports/training_process_review.md`)
- Optional MLB Tier A on pre-2021 + Tier C 2021 (fresh)
- NBA SBRO 2022–23 patch
- Live CloseLine from market close (not just omitting finals)
