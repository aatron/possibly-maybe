# Book round 2 — progress tracker

Started 2026-10-28. Baseline `main` @ `0f43ea7` (user WIP uncommitted in `PaperLedger.cs`, `ui/*` — do not touch/commit).
Final write-up → `reports/book_topics_review.md`.

## ▶ RESUME HERE (read after any interruption)

**Next action (2026-11-07):** two Sonnet agents running — `nba-normal-sigma-pover` merge-main + `daily-refresh-all-leagues`. When both report: review diff, `dotnet test`, merge into main, run `bash scripts/pm-calculate.sh`, update ledger. Then phase 5/6.

**Previous:** Phase 4b hunts done (NFL/MLB FAIL with filled O→C). Settle-CLV script fixed (no PaperLedger). Phase 5/6 write-ups still pending. When a session restarts, background agents from the old session are gone:
for each row still `running`, check whether its output file exists and is complete (ends with a shortlist/summary).
- File complete → mark `done`, commit it.
- File missing/partial → relaunch that one agent with the prompt in `agent_prompts.md` (same file name; Auto).
Never re-run a `done` row. Tier C soccer 2025–26 scored once for poisson (see ledger) — never re-score.
Code-change agents (phase 4+): Composer. Research/eval/audit: Auto.

### Agent ledger

| ID | Task | Model | Output file | Status |
|---|---|---|---|---|
| E1 | ISL / ESL / Sparsity topics | Auto | `eval_isl_esl_sparsity.md` | done — `78cd220d-6dea-4f92-9be9-8e6fde8283c7` |
| E2 | Bayesian (BDA3, ROS, CASI, Rethinking, BMCP) | Auto | `eval_bayesian.md` | done — `ccb38f53-6076-4767-9446-d235e223b2c8` |
| E3 | General ML (Murphy, Bishop, MacKay, MML, GPML) | Auto | `eval_ml_general.md` | done — `c89ab46e-e292-4565-bdcb-ea33624dcd1e` |
| E4 | Forecasting / markets (fpp3, conformal, scoring rules, Kaunitz, penaltyblog, Thorp) | Auto | `eval_forecast_markets.md` | done — `1e7297b3-a373-4afe-b281-1a1718342cd3` |
| A1 | Overcorrection / training audit | Auto | `audit_overcorrection.md` | done — `7751b52c-3e9d-4e0e-89de-02e2dd285307` |
| V1 | Venue placement (Kalshi / RH / Crypto.com) | Auto | `venue_placement.md` | done — `0f2e5c1d-9b92-42fc-be9d-ed68197fca64` |
| D1 | NBA/NFL/MLB odds data sources (download only → `data/raw/round2_odds/`) | Auto | `hunt_data_sources.md` | done — `b211375a-626d-4f98-97ac-5b8ff9c52719` |

### Phase 4 picks (merged E1–E4 + A1/V1; money × testability × low overfit)

| # | Slug | Why | Params | Source |
|---|---|---|---|---|
| 1 | `poisson-cdf-pover` | Soccer: replace logistic OverProb with Poisson/grid from existing λs — unanimous #1 | 0 | E1–E4 |
| 2 | `nba-normal-sigma-pover` | NBA: Normal CDF with train residual σ — fixes overconfident Kelly (σ≈22.8 vs sharpness 0.25) | ≤1 | E1–E4 |
| 3 | `venue-fee-ev` | Fee-adjusted buy price in EV/Kelly (Kalshi/Crypto/RH) — stops false GOs near even | formula | V1 |
| 4 | `strike-max-delta` | Reject Kalshi/Crypto strikes far from model line | 1 threshold | V1 |
| 5 | `inv-var-blend-w` | Inverse-variance model+line blend vs fixed w | ≤2 σ | E3 |
| — | `settle-clv-fix` | Live CloseLine=finals → fake CLV | script (+ maybe PaperLedger) | A1 — **blocked: ask user** |

Deferred: NB φ / DC ρ / conformal / Kaunitz / Kalman / train-sharpness (after #1–2 or higher overfit).

### Experiment ledger (phase 4 / 4b)

| Slug | Branch / worktree | Model | Tier A | Tier B | Tier C | Decision | Merge commit |
|---|---|---|---|---|---|---|---|
| poisson-cdf-pover | `../pm-exp-poisson-cdf-pover` `exp/poisson-cdf-pover` | Composer `8220e1bf-5332-4c2f-9cf8-aaed1e4a8eb5` | PASS SERIEA/LIGUE1/LIGAMX; FAIL MLS | — | KEEP SERIEA/LIGUE1; FAIL LIGAMX | KEEP (per-league) | merged |
| nba-normal-sigma-pover | `../pm-exp-nba-normal-sigma-pover` `exp/nba-normal-sigma-pover` | Composer → Sonnet (merge) | FAIL (0/8 ΔPnL; Kelly worse) | report only | — | **USER OVERRIDE 2026-11-07: merged** (same GO set; P 0.86→0.64, EV 63%→23% at 7pt edge; 64/64 tests) | ac8d180 |
| daily-refresh-all-leagues | `../pm-fix-daily-refresh` `fix/daily-refresh` | Sonnet | n/a (ops) | — | — | USER 2026-11-07: daily script refreshes every league + team aliases + stale-data guard | in progress |
| line-tracker-nfl-mlb | `../pm-feat-line-tracker` `feat/line-tracker` | Sonnet | n/a (ops: start O→C history) | — | — | USER 2026-11-07: track NFL + MLB lines (DK/ESPN, Kalshi, Crypto.com) going forward | in progress |
| venue-fee-ev | `../pm-exp-venue-fee-ev` `exp/venue-fee-ev` | Composer `5d87e728-6e42-4062-8268-c8093b21d395` | N/A (no hist binary asks) | N/A | N/A | KEEP/SHIP | merged |
| strike-max-delta | `../pm-exp-strike-max-delta` `exp/strike-max-delta` | Composer `9148a074-d970-4e38-8869-d46182183b53` | N/A (live hygiene) | N/A | N/A | KEEP/SHIP | merged |
| inv-var-blend-w | `../pm-exp-inv-var-blend-w` `exp/inv-var-blend-w` | Composer `d526bf5c-8ec4-4562-83df-fae1dd8d753c` | FAIL | — | — | KILL (unmerged) | — |

### Tier C ledger (look-once; append before scoring, never re-score)

| Date | Candidates scored | Result file |
|---|---|---|
| 2026-10-28 | `poisson-cdf-pover` on SERIEA / LIGUE1 / LIGAMX only (MLS Tier A FAIL — not scored). Holdout 2025-2026. | `tier_c_poisson-cdf-pover.md` |

## Phases

| # | Phase | Status | Output |
|---|---|---|---|
| 1 | Download free books / repos → `library/` | done | see Downloads |
| 2 | Topic evaluation (4 subagents, by book group) | done | `eval_*.md` |
| 3a | Over-correction / training audit | done — protocol tightened in PLAN | `audit_overcorrection.md` |
| 3b | Venue placement research (Kalshi / Robinhood / Crypto.com) | done | `venue_placement.md` |
| 4 | Pick candidates, worktree experiments | done — see experiment ledger | `exp_*.md` |
| 4b | NBA / NFL / MLB soft-spot hunt (data first, then models) | done — NFL/MLB FAIL; NBA 2022–23 SBRO not imported | `hunt_*.md` |
| 5 | Merge winners, write final review | pending | `reports/book_topics_review.md` |
| 6 | (very end) Training-practice review across all books → check our process, fix if needed | pending | `reports/training_process_review.md` |

Plan: `PLAN.md` (this folder).

## Safety scan (prompt injection) — clean

Full-text regex over all PDFs, `articles/*.md`, repo docs/code for AI-directed instructions, chat-template tokens, hidden unicode, agent config files.
Hits were benign: "as an aid" false positives; `<|im_start|>` in pyprobml's own LLM-training script; zero-width spaces in Kaunitz PDF title (export artifact, stripped). No tag-unicode, no CLAUDE.md/AGENTS.md/.cursor in repos.

## Data finding

Totals odds with an **unused 2025–26 season** exist for LIGUE1 / LIGAMX / SERIEA / LALIGA / EFL (+ MLS 2026 partial) → fresh look-once holdout (Tier C). NBA totals odds only 2008–2022 + 2025 → NBA changes provisional.

## Downloads

- New PDFs: ISLP, ISLR2, Kaunitz (arXiv 1710.02824), Angelopoulos–Bates conformal (2107.07511), Gneiting–Raftery scoring rules
- Already present: BDA3, PRML, CASI, ESL, MacKay, MML, Murphy PML1+2, GPML, ROS, Sparsity, Thorp Kelly
- Repos (`library/repos/`, shallow): penaltyblog, ROS-Examples, pymc-resources (Rethinking ports), stat_rethinking_2024, BookCode_Edition1 (BMCP), fpp3, conformal-prediction, pyprobml
- Not downloaded: Statistical Rethinking book & Cameron–Trivedi (paid, no free copy); ABDWR3e repo (URL wrong; MLB already FAIL)
- TOC text (first 30 pages): `library/toc/*.txt`

## Early findings (parent, before agents)

- Holdout 2024–25 reused for every KEEP decision → selection bias risk (audit item).
- `paper.db` `CloseLine`/`ClvPts` on settled tickets look like **final total goals**, not closing lines (COL@SJ CloseLine=4.0) → live CLV may be meaningless.
- 2026-10-28 day: 4/6 ≈ break-even because Kalshi wins were at −163/−150 while losses were near even.
- New DK tickets all logged at −110 — check whether that's the real DK price or a default.

## Log

| When | Event |
|---|---|
| 2026-10-28 | Phase 1 done; phases 2/3 agents launched |
| 2026-10-28 | Plan committed (a078fb8); 6 agents launched (E1–E4, audit, venue). User added phase 4b: NBA/NFL/MLB soft-spot hunt |
| 2026-10-28 | User: code-change agents run on Sonnet; 4b data-source agent launched (download only) |
| 2026-10-28 | User: research agents run on Opus (the 7 running agents already inherit Opus) |
| 2026-10-28 | Added RESUME HERE section, agent/experiment/Tier C ledgers, agent_prompts.md for relaunch |
| 2026-10-28 | Handoff doc added; pointers in AGENTS.md and root PROGRESS.md |
| 2026-10-28 | **PAUSED by user.** All 7 agents stopped; none had written output; data/raw/round2_odds not created; no worktrees. Clean state at this commit. |
| 2026-10-28 | Model directive change: code agents = Composer (was Sonnet); research = Auto (was Opus). Relaunched all 7 research agents (Auto, background). |
| 2026-10-28 | E1 done: `eval_isl_esl_sparsity.md`. Top $: soccer Poisson CDF OverProb (0 params); NBA residual-σ Normal CDF. |
| 2026-10-28 | E4 done: `eval_forecast_markets.md`. Agrees #1 Poisson/grid P(over); Kaunitz live-only (no TOTAL multi-book). |
| 2026-10-28 | A1 done: holdout reused ≥8×; live CloseLine=finals; DK −110 default; protocol tightened in PLAN. Money-fix rank: settle CLV → holdout peek → thin champs. |
| 2026-10-28 | E3 done: `eval_ml_general.md`. Same #1 distributional OverProb; NBA logistic badly overconfident vs residual σ≈22.8. |
| 2026-10-28 | V1 done: `venue_placement.md`. Top code gaps: fee-adjusted BE, exact strike gate, EU/MX Kalshi series. |
| 2026-10-28 | E2 done: `eval_bayesian.md`. Same #1 poisson-cdf-pover. Phase 2+3a gate clear → phase 4 picks locked. |
| 2026-10-28 | D1 progress: SBRO NFL xlsx 2007–08…2021–22 downloaded; NBA CDX shows through 2022–23; MLB CDX still 504. |
| 2026-10-28 | `strike-max-delta` KEEP/SHIP merged (live hygiene; N_trials=1; Tier A N/A). |
| 2026-10-28 | `venue-fee-ev` KEEP/SHIP merged (fee into EV/Kelly; N_trials=2; Tier A N/A). User WIP PaperLedger/ui restored cleanly. |
| 2026-10-28 | D1 done: `hunt_data_sources.md`. P0: NFL SBRO 2007–21 + MLB flancast/Arnav O→C fill prior FAILs; NBA only 2022–23 free O→C patch. |
| 2026-10-28 | `inv-var-blend-w` KILL — Tier A FAIL NBA 7/14 PnL folds, MLS 2/5; branch unmerged; exp doc on main. |
| 2026-10-28 | `poisson-cdf-pover` Tier A PASS SerieA/Ligue1/LigaMX (FAIL MLS); **await Tier C batch** before merge. |
| 2026-10-28 | Tier C poisson: KEEP SERIEA+LIGUE1; LIGAMX FAIL CLV. nba-normal-sigma KILL. |
| 2026-10-29 | Settle hint: `pm-calculate.sh` no longer passes final_total as closeLine. |
| 2026-10-29 | `import-round2-odds`: NFL +2892 O→C overlays; MLB +25571 games. Hunts: NFL FAIL, MLB FAIL (docs). |
| 2026-11-07 | Replay 10-30→11-06 (38 picks, 18–20, −3.64u flat) → `reports/replay_2026-10-30_to_11-06.md`. Found: soccer DB has no 2026-27 games; Le Mans→Metz mapping bug; Serie A DC not credible. Settled 4 open DK paper tickets (2W 2L). |
| 2026-11-07 | User: daily script must refresh every league; merge NBA Normal-σ curve (override KILL); list planned-not-implemented. Two Sonnet agents launched. |
| 2026-11-07 | User: start tracking NFL/MLB lines; asked about college sports + bet sizing. Line-tracker Sonnet agent launched. |
| 2026-11-07 | NBA Normal-σ merged (ac8d180), tests 64/64. |
